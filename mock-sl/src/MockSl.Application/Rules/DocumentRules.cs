using MockSl.Application.Entities;
using MockSl.Domain.Documents;
using MockSl.Domain.Errors;
using MockSl.Domain.Metadata;

namespace MockSl.Application.Rules;

/// <summary>
/// The life of a sales or purchase document. DocumentFlow holds what differs between the two.
/// Calculations live in Domain; this class loads what they need and saves what they decide.
/// </summary>
public sealed class DocumentRules(EntityDefinition entity, DocumentFlow flow, TimeProvider clock) : EntityRules
{
    private static readonly EntityDefinition Partners = EntityCatalog.BusinessPartners;
    private static readonly EntityDefinition Items = EntityCatalog.Items;
    private static readonly EntityDefinition Employees = EntityCatalog.EmployeesInfo;

    private readonly ChildDefinition _lines = entity.Child("DocumentLines");
    private readonly ChildDefinition? _installments = entity.FindChild("DocumentInstallments");

    public override async Task BeforeInsertAsync(
        EntityDraft draft,
        IEntitySession session,
        CancellationToken cancellationToken)
    {
        var header = draft.Header;

        await SetPartnerNameAsync(header, session, cancellationToken);
        await EnsureOwnerExistsAsync(header, session, cancellationToken);

        var docDate = header.Date("DocDate") ?? DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var dueDate = header.Date("DocDueDate") ?? docDate;
        header.Set(entity.Field("DocDate"), docDate);
        header.Set(entity.Field("DocDueDate"), dueDate);

        var lines = draft.Rows(_lines);
        if (lines.Count == 0)
        {
            throw SapErrors.DocumentWithoutLines();
        }

        foreach (var line in lines)
        {
            await PrepareLineAsync(line, session, cancellationToken);
        }

        var total = DocumentMath.DocumentTotal(lines.Select(line => line.Decimal("LineTotal")!.Value));
        header.Set(entity.Field("DocTotal"), total);

        if (_installments is not null)
        {
            SetInstallments(draft, _installments, total, dueDate);
        }
    }

    public override async Task AfterInsertAsync(
        EntityDraft draft,
        EntityRow row,
        IEntitySession session,
        CancellationToken cancellationToken)
    {
        var stockLines = draft.Rows(_lines).Select(line => new StockLine(
            line.Text("ItemCode")!,
            line.Text("WhsCode")!,
            line.Decimal("Quantity")!.Value));

        await StockMovement.ApplyAsync(session, stockLines, flow.StockSign, cancellationToken);
        await MovePartnerBalanceAsync(row, flow.BalanceSign, session, cancellationToken);
    }

    public override Task RunActionAsync(
        string action,
        IReadOnlyList<FieldValue> key,
        IEntitySession session,
        CancellationToken cancellationToken) => action switch
        {
            "Cancel" => CancelAsync(key, session, cancellationToken),
            "Close" => CloseAsync(key, session, cancellationToken),
            _ => throw SapErrors.ActionNotSupported(),
        };

    /// <summary>Undoes everything the document did: stock and balance return to where they were.</summary>
    private async Task CancelAsync(
        IReadOnlyList<FieldValue> key,
        IEntitySession session,
        CancellationToken cancellationToken)
    {
        var document = await session.FindForUpdateAsync(entity, key, cancellationToken)
            ?? throw SapErrors.EntityNotFound();

        DocumentLifecycle.EnsureCanCancel(document.Text("CANCELED")!, document.Text("DocStatus")!, document.Decimal("PaidToDate"));

        var docEntry = document["DocEntry"];
        var lines = await session.QueryChildrenAsync(_lines, [docEntry], cancellationToken);
        var stockLines = lines.Select(line => new StockLine(line.Text("ItemCode")!, line.Text("WhsCode")!, line.Decimal("Quantity")));

        await StockMovement.ApplyAsync(session, stockLines, -flow.StockSign, cancellationToken);
        await MovePartnerBalanceAsync(document, -flow.BalanceSign, session, cancellationToken);

        await session.UpdateAsync(
            entity,
            key,
            [
                new FieldValue(entity.Field("Cancelled"), DocumentLifecycle.Yes),
                new FieldValue(entity.Field("DocumentStatus"), DocumentLifecycle.Closed),
            ],
            cancellationToken);

        await CloseChildrenAsync(docEntry, session, cancellationToken);
    }

    /// <summary>Stops the document being open. Unlike Cancel, stock and balance stay as they are.</summary>
    private async Task CloseAsync(
        IReadOnlyList<FieldValue> key,
        IEntitySession session,
        CancellationToken cancellationToken)
    {
        var document = await session.FindForUpdateAsync(entity, key, cancellationToken)
            ?? throw SapErrors.EntityNotFound();

        DocumentLifecycle.EnsureCanClose(document.Text("CANCELED")!, document.Text("DocStatus")!);

        await session.UpdateAsync(
            entity,
            key,
            [new FieldValue(entity.Field("DocumentStatus"), DocumentLifecycle.Closed)],
            cancellationToken);

        await CloseChildrenAsync(document["DocEntry"], session, cancellationToken);
    }

    private async Task CloseChildrenAsync(object? docEntry, IEntitySession session, CancellationToken cancellationToken)
    {
        await session.UpdateAllChildrenAsync(
            _lines,
            docEntry,
            [new FieldValue(_lines.Field("LineStatus"), DocumentLifecycle.Closed)],
            cancellationToken);

        if (_installments is not null)
        {
            await session.UpdateAllChildrenAsync(
                _installments,
                docEntry,
                [new FieldValue(StoredOnlyColumns.InstallmentStatus, DocumentLifecycle.Closed)],
                cancellationToken);
        }
    }

    private async Task SetPartnerNameAsync(DraftRow header, IEntitySession session, CancellationToken cancellationToken)
    {
        var cardCode = header.Text("CardCode")!;

        var partner = await session.FindAsync(
            Partners,
            EntityKey.Of(Partners, cardCode),
            [Partners.Field("CardName"), Partners.Field("CardType")],
            cancellationToken)
            ?? throw SapErrors.ReferenceNotFound("CardCode", cardCode);

        if (partner.Text("CardType") != flow.PartnerCardType)
        {
            throw SapErrors.WrongPartnerType(cardCode);
        }

        header.Set(entity.Field("CardName"), partner.Text("CardName"));
    }

    private static async Task EnsureOwnerExistsAsync(DraftRow header, IEntitySession session, CancellationToken cancellationToken)
    {
        if (header.Int("OwnerCode") is not { } owner)
        {
            return;
        }

        var employee = await session.FindAsync(Employees, EntityKey.Of(Employees, owner), Employees.Key, cancellationToken);
        if (employee is null)
        {
            throw SapErrors.ReferenceNotFound("DocumentsOwner", owner.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    private async Task PrepareLineAsync(DraftRow line, IEntitySession session, CancellationToken cancellationToken)
    {
        var itemCode = line.Text("ItemCode")!;

        var item = await session.FindAsync(Items, EntityKey.Of(Items, itemCode), [Items.Field("ItemName")], cancellationToken)
            ?? throw SapErrors.ReferenceNotFound("ItemCode", itemCode);

        var price = line.Decimal("Price") ?? throw SapErrors.RequiredProperty(_lines.TypeName, "UnitPrice");
        var quantity = line.Decimal("Quantity")!.Value;

        line.Set(_lines.Field("LineTotal"), DocumentMath.LineTotal(quantity, price));
        line.Set(_lines.Field("WarehouseCode"), line.Text("WhsCode") ?? StockRules.DefaultWarehouse);

        if (line.Text("Dscription") is null)
        {
            line.Set(_lines.Field("ItemDescription"), item.Text("ItemName"));
        }
    }

    private void SetInstallments(EntityDraft draft, ChildDefinition installments, decimal total, DateOnly dueDate)
    {
        var inputs = draft.Rows(installments)
            .Select(row => new InstallmentInput(row.Date("DueDate"), row.Decimal("InsTotal"), row.Decimal("InstPrcnt")))
            .ToList();

        var plan = InstallmentPlan.Build(total, dueDate, inputs);

        var rows = plan.Select(installment =>
        {
            var row = new DraftRow();
            row.Set(installments.Field("DueDate"), installment.DueDate);
            row.Set(installments.Field("Total"), installment.Total);
            row.Set(installments.Field("Percentage"), installment.Percentage);
            return row;
        }).ToList();

        draft.SetRows(installments, rows);
        draft.Header.Set(entity.Field("NumberOfInstallments"), plan.Count);
    }

    private static Task MovePartnerBalanceAsync(
        EntityRow document,
        int sign,
        IEntitySession session,
        CancellationToken cancellationToken) =>
        session.AddToNumberAsync(
            Partners,
            EntityKey.Of(Partners, document.Text("CardCode")!),
            Partners.Field("CurrentAccountBalance"),
            sign * document.Decimal("DocTotal"),
            cancellationToken);
}
