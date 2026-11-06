using MockSl.Application.Entities;
using MockSl.Application.OData;
using MockSl.Domain.Errors;
using MockSl.Domain.Metadata;

namespace MockSl.Application.Rules;

/// <summary>
/// A user-defined field is two things that must stay together: a row in CUFD and a column in the table.
/// Both happen in one transaction: PostgreSQL can roll back ALTER TABLE too.
/// </summary>
public sealed class UserFieldRules : EntityRules
{
    private static readonly EntityDefinition Metadata = EntityCatalog.UserFieldsMD;

    public override async Task BeforeInsertAsync(
        EntityDraft draft,
        IEntitySession session,
        CancellationToken cancellationToken)
    {
        var header = draft.Header;
        var table = header.Text("TableID")!;
        var name = header.Text("AliasID")!;
        var type = header.Text("TypeID")!;

        // These two values end up inside the DDL statement, so they are checked before any SQL runs.
        UserFieldPolicy.EnsureAllowedTable(table);
        UserFieldPolicy.EnsureValidName(name);
        var editSize = UserFieldPolicy.ResolveEditSize(type, header.Int("EditSize"), header.Int("SizeID"));

        // ALTER TABLE locks the table until this transaction ends, so a parallel request for the
        // same table waits here. FieldID is read after that point, or both could take one number.
        await session.AddColumnAsync(table, UserFieldPolicy.ToField(name, type, editSize), cancellationToken);
        var fieldIds = await ReadFieldIdsOfTableAsync(table, session, cancellationToken);

        header.Set(Metadata.Field("FieldID"), UserFieldPolicy.NextFieldId(fieldIds));
        header.Set(Metadata.Field("Size"), editSize);
        header.Set(Metadata.Field("EditSize"), editSize);
        header.Set(Metadata.Field("SubType"), header.Text("EditType") ?? string.Empty);
    }

    /// <summary>The guide lets a client change the description and the size (p.88), not the type.</summary>
    public override async Task BeforeUpdateAsync(
        IReadOnlyList<FieldValue> key,
        DraftRow changes,
        IEntitySession session,
        CancellationToken cancellationToken)
    {
        if (changes.Int("EditSize") is not { } editSize)
        {
            return;
        }

        var field = await session.FindForUpdateAsync(Metadata, key, cancellationToken)
            ?? throw SapErrors.EntityNotFound();

        var type = field.Text("TypeID")!;
        UserFieldPolicy.EnsureCanResize(type, field.Int("EditSize"), editSize);

        if (UserFieldPolicy.IsText(type))
        {
            await session.ResizeColumnAsync(field.Text("TableID")!, ColumnOf(field), editSize, cancellationToken);
        }

        changes.Set(Metadata.Field("Size"), editSize);
    }

    public override async Task BeforeDeleteAsync(
        IReadOnlyList<FieldValue> key,
        IEntitySession session,
        CancellationToken cancellationToken)
    {
        var field = await session.FindForUpdateAsync(Metadata, key, cancellationToken)
            ?? throw SapErrors.EntityNotFound();

        await session.DropColumnAsync(field.Text("TableID")!, ColumnOf(field), cancellationToken);
    }

    private static string ColumnOf(EntityRow field) => $"{UserFieldPolicy.ColumnPrefix}{field.Text("AliasID")}";

    private static async Task<IEnumerable<int>> ReadFieldIdsOfTableAsync(
        string table,
        IEntitySession session,
        CancellationToken cancellationToken)
    {
        var sameTable = new ComparisonNode(Metadata.Field("TableName"), ComparisonOperator.Equal, table);
        var columns = new[] { Metadata.Field("TableName"), Metadata.Field("FieldID") };
        var rows = await session.QueryAsync(Metadata, new EntityQuery(columns, sameTable, [], null, 0), cancellationToken);
        return rows.Select(row => row.Int("FieldID"));
    }
}
