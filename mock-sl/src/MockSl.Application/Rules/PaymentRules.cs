using MockSl.Application.Entities;
using MockSl.Domain.Documents;
using MockSl.Domain.Errors;
using MockSl.Domain.Metadata;

namespace MockSl.Application.Rules;

/// <summary>
/// The life of a payment. PaymentFlow holds what differs between incoming and outgoing.
/// Calculations live in Settlement; this class loads what they need and saves what they decide.
/// </summary>
public sealed class PaymentRules(EntityDefinition entity, PaymentFlow flow, TimeProvider clock) : EntityRules
{
    private static readonly EntityDefinition Partners = EntityCatalog.BusinessPartners;
    private static readonly EntityDefinition Accounts = EntityCatalog.ChartOfAccounts;
    private static readonly EntityDefinition Periods = EntityCatalog.PeriodCategories;

    private readonly ChildDefinition _applied = entity.Child("PaymentInvoices");

    public override async Task BeforeInsertAsync(
        EntityDraft draft,
        IEntitySession session,
        CancellationToken cancellationToken)
    {
        var header = draft.Header;

        await SetPartnerNameAsync(header, session, cancellationToken);

        var cashSum = header.Decimal("CashSum")!.Value;
        if (cashSum <= 0)
        {
            throw SapErrors.AmountMustBePositive("CashSum");
        }

        header.Set(entity.Field("DocDate"), header.Date("DocDate") ?? DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime));
        header.Set(entity.Field("DocType"), flow.DocType);
        header.Set(StoredOnlyColumns.PaymentDocTotal, cashSum);
        header.Set(entity.Field("CashAccount"), await ResolveCashAccountAsync(header.Text("CashAcct"), session, cancellationToken));

        var rows = draft.Rows(_applied);
        foreach (var row in rows)
        {
            SetInvoiceType(row);
            await EnsureInvoiceExistsAsync(row.Int("DocEntry")!.Value, session, cancellationToken);
        }

        if (rows.Sum(row => row.Decimal("SumApplied")!.Value) > cashSum)
        {
            throw SapErrors.AppliedExceedsPayment();
        }
    }

    public override async Task AfterInsertAsync(
        EntityDraft draft,
        EntityRow row,
        IEntitySession session,
        CancellationToken cancellationToken)
    {
        var cardCode = row.Text("CardCode")!;

        // The same order everywhere, so two payments cannot deadlock on each other's documents.
        foreach (var applied in draft.Rows(_applied).OrderBy(applied => applied.Int("DocEntry")))
        {
            await ApplyToInvoiceAsync(
                applied.Int("DocEntry")!.Value,
                cardCode,
                applied.Decimal("SumApplied")!.Value,
                applied.Int("InstId"),
                session,
                cancellationToken);
        }

        await MoveBalancesAsync(row, sign: +1, session, cancellationToken);
    }

    public override Task RunActionAsync(
        string action,
        IReadOnlyList<FieldValue> key,
        IEntitySession session,
        CancellationToken cancellationToken) => action switch
        {
            "Cancel" => CancelAsync(key, session, cancellationToken),
            _ => throw SapErrors.ActionNotSupported(),
        };

    private async Task CancelAsync(
        IReadOnlyList<FieldValue> key,
        IEntitySession session,
        CancellationToken cancellationToken)
    {
        var payment = await session.FindForUpdateAsync(entity, key, cancellationToken)
            ?? throw SapErrors.EntityNotFound();

        if (payment.Text("Canceled") != DocumentLifecycle.No)
        {
            throw SapErrors.DocumentAlreadyCancelled();
        }

        var applied = await session.QueryChildrenAsync(_applied, [payment["DocEntry"]], cancellationToken);
        foreach (var row in applied.OrderBy(row => row.Int("DocEntry")))
        {
            await ReverseFromInvoiceAsync(row.Int("DocEntry"), row.Decimal("SumApplied"), session, cancellationToken);
        }

        await MoveBalancesAsync(payment, sign: -1, session, cancellationToken);

        await session.UpdateAsync(
            entity,
            key,
            [new FieldValue(entity.Field("Cancelled"), DocumentLifecycle.Yes)],
            cancellationToken);
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

    /// <summary>Without an account in the request, OACP says which one takes cash receipts.</summary>
    private static async Task<string> ResolveCashAccountAsync(
        string? requested,
        IEntitySession session,
        CancellationToken cancellationToken)
    {
        var code = requested ?? await FindDefaultCashAccountAsync(session, cancellationToken)
            ?? throw SapErrors.CashAccountRequired();

        var account = await session.FindAsync(
            Accounts,
            EntityKey.Of(Accounts, code),
            [Accounts.Field("ActiveAccount")],
            cancellationToken)
            ?? throw SapErrors.ReferenceNotFound("CashAccount", code);

        return account.Text("Postable") == DocumentLifecycle.Yes ? code : throw SapErrors.AccountNotPostable(code);
    }

    private static async Task<string?> FindDefaultCashAccountAsync(IEntitySession session, CancellationToken cancellationToken)
    {
        var query = new EntityQuery([Periods.Field("AccountforCashReceipt")], null, [], 1, 0);
        var periods = await session.QueryAsync(Periods, query, cancellationToken);
        return periods.Count == 0 ? null : periods[0].Text("LinkAct_3");
    }

    // Checked before the payment rows are written, so a wrong number is a clear error and not a database failure.
    private async Task EnsureInvoiceExistsAsync(int docEntry, IEntitySession session, CancellationToken cancellationToken)
    {
        var invoices = flow.Invoices;
        var invoice = await session.FindAsync(invoices, EntityKey.Of(invoices, docEntry), invoices.Key, cancellationToken);

        if (invoice is null)
        {
            throw SapErrors.ReferenceNotFound("DocEntry", docEntry.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    private void SetInvoiceType(DraftRow row)
    {
        var field = _applied.Field("InvoiceType");
        var invoiceType = row.Text(field.Column) ?? flow.InvoiceType;

        if (invoiceType != flow.InvoiceType)
        {
            throw SapErrors.WrongInvoiceType(field.Enum!.GetName(flow.InvoiceType));
        }

        row.Set(field, invoiceType);
    }

    private async Task ApplyToInvoiceAsync(
        int docEntry,
        string cardCode,
        decimal applied,
        int? installmentId,
        IEntitySession session,
        CancellationToken cancellationToken)
    {
        var invoices = flow.Invoices;
        var key = EntityKey.Of(invoices, docEntry);

        var invoice = await session.FindForUpdateAsync(invoices, key, cancellationToken)
            ?? throw SapErrors.ReferenceNotFound("DocEntry", docEntry.ToString(System.Globalization.CultureInfo.InvariantCulture));

        var change = Settlement.Apply(ToState(invoice), cardCode, applied);
        await SaveInvoiceAsync(invoices, key, invoice, change, session, cancellationToken);

        if (invoices.FindChild("DocumentInstallments") is { } installments)
        {
            var rows = await session.QueryChildrenAsync(installments, [docEntry], cancellationToken);
            var changes = Settlement.AllocateToInstallments(ToInstallmentStates(rows), applied, installmentId);
            await SaveInstallmentsAsync(installments, docEntry, changes, session, cancellationToken);
        }
    }

    private async Task ReverseFromInvoiceAsync(
        int docEntry,
        decimal applied,
        IEntitySession session,
        CancellationToken cancellationToken)
    {
        var invoices = flow.Invoices;
        var key = EntityKey.Of(invoices, docEntry);

        var invoice = await session.FindForUpdateAsync(invoices, key, cancellationToken)
            ?? throw SapErrors.ReferenceNotFound("DocEntry", docEntry.ToString(System.Globalization.CultureInfo.InvariantCulture));

        var change = Settlement.Reverse(ToState(invoice), applied);
        await SaveInvoiceAsync(invoices, key, invoice, change, session, cancellationToken);

        if (invoices.FindChild("DocumentInstallments") is { } installments)
        {
            var rows = await session.QueryChildrenAsync(installments, [docEntry], cancellationToken);
            var changes = Settlement.ReverseFromInstallments(ToInstallmentStates(rows), applied);
            await SaveInstallmentsAsync(installments, docEntry, changes, session, cancellationToken);
        }
    }

    private static async Task SaveInvoiceAsync(
        EntityDefinition invoices,
        IReadOnlyList<FieldValue> key,
        EntityRow before,
        InvoiceChange change,
        IEntitySession session,
        CancellationToken cancellationToken)
    {
        await session.UpdateAsync(
            invoices,
            key,
            [
                new FieldValue(invoices.Field("PaidToDate"), change.PaidToDate),
                new FieldValue(invoices.Field("DocumentStatus"), change.Status),
            ],
            cancellationToken);

        if (change.Status != before.Text("DocStatus"))
        {
            var lines = invoices.Child("DocumentLines");
            await session.UpdateAllChildrenAsync(
                lines,
                before["DocEntry"],
                [new FieldValue(lines.Field("LineStatus"), change.Status)],
                cancellationToken);
        }
    }

    private static async Task SaveInstallmentsAsync(
        ChildDefinition installments,
        int docEntry,
        IReadOnlyList<InstallmentChange> changes,
        IEntitySession session,
        CancellationToken cancellationToken)
    {
        foreach (var change in changes)
        {
            await session.UpdateChildAsync(
                installments,
                docEntry,
                change.Id,
                [
                    new FieldValue(StoredOnlyColumns.InstallmentPaidToDate, change.PaidToDate),
                    new FieldValue(StoredOnlyColumns.InstallmentStatus, change.Status),
                ],
                cancellationToken);
        }
    }

    private async Task MoveBalancesAsync(EntityRow payment, int sign, IEntitySession session, CancellationToken cancellationToken)
    {
        var cashSum = payment.Decimal("CashSum");

        await session.AddToNumberAsync(
            Partners,
            EntityKey.Of(Partners, payment.Text("CardCode")!),
            Partners.Field("CurrentAccountBalance"),
            sign * flow.BalanceSign * cashSum,
            cancellationToken);

        await session.AddToNumberAsync(
            Accounts,
            EntityKey.Of(Accounts, payment.Text("CashAcct")!),
            Accounts.Field("Balance"),
            sign * flow.CashSign * cashSum,
            cancellationToken);
    }

    private static InvoiceState ToState(EntityRow invoice) => new(
        invoice.Int("DocEntry"),
        invoice.Text("CardCode")!,
        invoice.Text("CANCELED")!,
        invoice.Text("DocStatus")!,
        invoice.Decimal("DocTotal"),
        invoice.Decimal("PaidToDate"));

    private static List<InstallmentState> ToInstallmentStates(IEnumerable<EntityRow> rows) =>
        rows.Select(row => new InstallmentState(row.Int("InstlmntID"), row.Decimal("InsTotal"), row.Decimal("PaidToDate"))).ToList();
}
