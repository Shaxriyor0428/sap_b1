using System.Text.Json;
using System.Text.Json.Nodes;
using MockSl.Application.OData;
using MockSl.Application.Rules;
using MockSl.Domain.Errors;
using MockSl.Domain.Metadata;

namespace MockSl.Application.Entities;

public sealed record EntityPage(JsonArray Value, NextPage? Next);

public sealed record CreatedEntity(JsonObject Value, string KeyInUrl);

/// <summary>
/// The one generic handler: list, get, create, update, delete and actions for any object.
/// Fields come from the EntityDefinition, business rules from RulesCatalog.
/// Every method is one transaction.
/// </summary>
public sealed class EntityHandler(IEntityStore store, RulesCatalog rules)
{
    /// <summary>
    /// Adds the user-defined fields as they are right now. It is read on every request,
    /// so a new field is usable at once and there is no cache to keep in step.
    /// </summary>
    public async Task<EntityDefinition> WithUserFieldsAsync(EntityDefinition entity, CancellationToken cancellationToken)
    {
        await using var session = await store.OpenSessionAsync(cancellationToken);

        var metadata = EntityCatalog.UserFieldsMD;
        var query = new EntityQuery(metadata.Fields, null, [], null, 0);
        var rows = await session.QueryAsync(metadata, query, cancellationToken);

        var fields = rows.ToLookup(
            row => row.Text("TableID")!,
            row => UserFieldPolicy.ToField(row.Text("AliasID")!, row.Text("TypeID")!, row.Int("EditSize")));

        return entity.WithUserFields(fields);
    }

    public async Task<EntityPage> ListAsync(
        EntityDefinition entity,
        ODataQuery query,
        PagePlan plan,
        CancellationToken cancellationToken)
    {
        await using var session = await store.OpenSessionAsync(cancellationToken);

        var entityQuery = new EntityQuery(ColumnsToRead(entity, query), query.Filter, query.OrderBy, plan.FetchLimit, plan.Skip);
        var rows = await session.QueryAsync(entity, entityQuery, cancellationToken);
        var visibleRows = (plan.RowLimit is { } limit ? rows.Take(limit) : rows).ToList();

        var related = await LoadRelatedAsync(session, entity, query.Children, query.Expand, visibleRows, cancellationToken);

        var value = new JsonArray();
        foreach (var row in visibleRows)
        {
            value.Add(ToJson(entity, query.Select, query.Children, query.Expand, row, related));
        }

        return new EntityPage(value, plan.NextPageAfter(rows.Count));
    }

    public async Task<JsonObject> GetAsync(
        EntityDefinition entity,
        IReadOnlyList<FieldValue> key,
        ODataQuery query,
        CancellationToken cancellationToken)
    {
        await using var session = await store.OpenSessionAsync(cancellationToken);

        var row = await session.FindAsync(entity, key, ColumnsToRead(entity, query), cancellationToken)
            ?? throw SapErrors.EntityNotFound();

        var related = await LoadRelatedAsync(session, entity, query.Children, query.Expand, [row], cancellationToken);
        return ToJson(entity, query.Select, query.Children, query.Expand, row, related);
    }

    public async Task<CreatedEntity> CreateAsync(
        EntityDefinition entity,
        JsonElement body,
        CancellationToken cancellationToken)
    {
        var draft = EntityBodyReader.ReadForCreate(entity, body);
        var entityRules = rules.For(entity);

        await using var session = await store.OpenSessionAsync(cancellationToken);

        await entityRules.BeforeInsertAsync(draft, session, cancellationToken);
        var row = await session.InsertAsync(entity, draft.Header.Values, cancellationToken);
        await InsertChildrenAsync(entity, draft, row, session, cancellationToken);
        await entityRules.AfterInsertAsync(draft, row, session, cancellationToken);

        // Read back, so the response shows what is really stored, including what the rules added.
        var related = await LoadRelatedAsync(session, entity, entity.Children, [], [row], cancellationToken);
        var json = ToJson(entity, entity.Fields, entity.Children, [], row, related);

        await session.CommitAsync(cancellationToken);
        return new CreatedEntity(json, EntityKey.Format(entity, row));
    }

    public async Task UpdateAsync(
        EntityDefinition entity,
        IReadOnlyList<FieldValue> key,
        JsonElement body,
        CancellationToken cancellationToken)
    {
        var changes = EntityBodyReader.ReadForUpdate(entity, body);

        await using var session = await store.OpenSessionAsync(cancellationToken);

        await rules.For(entity).BeforeUpdateAsync(key, changes, session, cancellationToken);

        // A body with only ignored properties changes nothing, but the object must still exist.
        var found = changes.Values.Count > 0
            ? await session.UpdateAsync(entity, key, changes.Values, cancellationToken)
            : await session.FindAsync(entity, key, entity.Key, cancellationToken) is not null;

        if (!found)
        {
            throw SapErrors.EntityNotFound();
        }

        await session.CommitAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        EntityDefinition entity,
        IReadOnlyList<FieldValue> key,
        CancellationToken cancellationToken)
    {
        if (!entity.CanDelete)
        {
            throw SapErrors.ActionNotSupported();
        }

        await using var session = await store.OpenSessionAsync(cancellationToken);

        await rules.For(entity).BeforeDeleteAsync(key, session, cancellationToken);

        if (!await session.DeleteAsync(entity, key, cancellationToken))
        {
            throw SapErrors.EntityNotFound();
        }

        await session.CommitAsync(cancellationToken);
    }

    /// <summary>Runs a bound action such as POST Invoices(1)/Cancel (guide p.29).</summary>
    public async Task RunActionAsync(
        EntityDefinition entity,
        IReadOnlyList<FieldValue> key,
        string action,
        CancellationToken cancellationToken)
    {
        await using var session = await store.OpenSessionAsync(cancellationToken);
        await rules.For(entity).RunActionAsync(action, key, session, cancellationToken);
        await session.CommitAsync(cancellationToken);
    }

    // The key and the foreign keys are needed to attach related rows, even when the client did not select them.
    // The key also keeps the list from being empty: $select=BusinessPartner names no column at all.
    private static List<FieldDefinition> ColumnsToRead(EntityDefinition entity, ODataQuery query)
    {
        var foreignKeys = query.Expand.Select(item => entity.Field(item.Navigation.ForeignKeyField));
        return query.Select.Concat(entity.Key).Concat(foreignKeys).Distinct().ToList();
    }

    private static async Task InsertChildrenAsync(
        EntityDefinition entity,
        EntityDraft draft,
        EntityRow parent,
        IEntitySession session,
        CancellationToken cancellationToken)
    {
        foreach (var child in draft.ChildrenWithRows)
        {
            var rowKey = child.FirstRowKey;

            foreach (var row in draft.Rows(child))
            {
                row.Set(child.RowKey, rowKey++);
                await session.InsertChildAsync(child, parent[entity.Key[0].Column], row.Values, cancellationToken);
            }
        }
    }

    private static async Task<RelatedRows> LoadRelatedAsync(
        IEntitySession session,
        EntityDefinition entity,
        IReadOnlyList<ChildDefinition> children,
        IReadOnlyList<ExpandItem> expand,
        IReadOnlyList<EntityRow> parents,
        CancellationToken cancellationToken)
    {
        var related = new RelatedRows();
        if (parents.Count == 0)
        {
            return related;
        }

        // One query per collection and per link for the whole page, not one per row.
        foreach (var child in children)
        {
            var parentKeys = parents.Select(parent => parent[entity.Key[0].Column]).ToList();
            var rows = await session.QueryChildrenAsync(child, parentKeys, cancellationToken);
            related.Children[child] = rows.ToLookup(row => row[child.ParentKeyColumn]);
        }

        foreach (var item in expand)
        {
            var target = item.Navigation.Target;
            var foreignKey = entity.Field(item.Navigation.ForeignKeyField).Column;
            var keys = parents.Select(parent => parent[foreignKey]).Where(key => key is not null).Distinct().ToList();
            var columns = item.Select.Union(target.Key).ToList();

            var rows = await session.FindManyAsync(target, keys, columns, cancellationToken);
            related.Expanded[item] = rows.ToDictionary(row => row[target.Key[0].Column]!);
        }

        return related;
    }

    private static JsonObject ToJson(
        EntityDefinition entity,
        IEnumerable<FieldDefinition> fields,
        IReadOnlyList<ChildDefinition> children,
        IReadOnlyList<ExpandItem> expand,
        EntityRow row,
        RelatedRows related)
    {
        var json = EntitySerializer.ToJson(fields, row);

        foreach (var child in children)
        {
            json[child.Name] = EntitySerializer.ToJsonArray(child, related.Children[child][row[entity.Key[0].Column]]);
        }

        foreach (var item in expand)
        {
            var foreignKey = row[entity.Field(item.Navigation.ForeignKeyField).Column];
            var linked = foreignKey is null ? null : related.Expanded[item].GetValueOrDefault(foreignKey);
            json[item.Navigation.Name] = linked is null ? null : EntitySerializer.ToJson(item.Select, linked);
        }

        return json;
    }

    private sealed class RelatedRows
    {
        public Dictionary<ChildDefinition, ILookup<object?, EntityRow>> Children { get; } = [];

        public Dictionary<ExpandItem, Dictionary<object, EntityRow>> Expanded { get; } = [];
    }
}
