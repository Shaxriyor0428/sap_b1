using MockSl.Application.Entities;
using MockSl.Application.SqlQueries;
using MockSl.Domain.Metadata;
using MockSl.Domain.SqlQueries;

namespace MockSl.Application.Rules;

/// <summary>
/// Saved SQL is checked when it is saved, as in guide p.154-157, and the normalized text
/// and the parameter names are stored instead of what the client sent (p.141, p.152).
/// </summary>
public sealed class SqlQueryRules(TimeProvider clock) : EntityRules
{
    private static readonly EntityDefinition Definition = EntityCatalog.SqlQueries;

    public override async Task BeforeInsertAsync(
        EntityDraft draft,
        IEntitySession session,
        CancellationToken cancellationToken)
    {
        await StoreNormalizedAsync(draft.Header.Text("SqlText")!, draft.Header, session, cancellationToken);

        draft.Header.Set(Definition.Field("CreateDate"), Today());
        draft.Header.Set(Definition.Field("UpdateDate"), Today());
    }

    public override async Task BeforeUpdateAsync(
        IReadOnlyList<FieldValue> key,
        DraftRow changes,
        IEntitySession session,
        CancellationToken cancellationToken)
    {
        if (changes.Text("SqlText") is { } sql)
        {
            await StoreNormalizedAsync(sql, changes, session, cancellationToken);
        }

        if (changes.Values.Count > 0)
        {
            changes.Set(Definition.Field("UpdateDate"), Today());
        }
    }

    private static async Task StoreNormalizedAsync(
        string sql,
        DraftRow row,
        IEntitySession session,
        CancellationToken cancellationToken)
    {
        var columns = await session.ReadTableColumnsAsync(SqlQueryPolicy.AllowedTables, cancellationToken);
        var normalized = SqlNormalizer.Normalize(sql, columns);

        row.Set(Definition.Field("SqlText"), normalized.SqlText);
        row.Set(Definition.Field("ParamList"), normalized.ParamList);
    }

    private DateOnly Today() => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
}
