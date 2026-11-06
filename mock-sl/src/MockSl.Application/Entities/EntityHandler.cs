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

        var children = await LoadChildrenAsync(session, entity, query.Children, visibleRows, cancellationToken);

        var value = new JsonArray();
        foreach (var row in visibleRows)
        {
            value.Add(ToJson(entity, query.Select, query.Children, row, children));
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

        var children = await LoadChildrenAsync(session, entity, query.Children, [row], cancellationToken);
        return ToJson(entity, query.Select, query.Children, row, children);
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
        var children = await LoadChildrenAsync(session, entity, entity.Children, [row], cancellationToken);
        var json = ToJson(entity, entity.Fields, entity.Children, row, children);

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

    // The key is needed to attach collections, even when the client did not select it.
    private static List<FieldDefinition> ColumnsToRead(EntityDefinition entity, ODataQuery query) =>
        query.Children.Count == 0 ? [.. query.Select] : [.. query.Select.Union(entity.Key)];

    private static async Task InsertChildrenAsync(
        EntityDefinition entity,
        EntityDraft draft,
        EntityRow parent,
        IEntitySession session,
        CancellationToken cancellationToken)
    {
        var parentKey = parent[entity.Key[0].Column];

        foreach (var child in draft.ChildrenWithRows)
        {
            var rowKey = child.FirstRowKey;

            foreach (var row in draft.Rows(child))
            {
                row.Set(child.RowKey, rowKey++);
                await session.InsertChildAsync(child, parentKey, row.Values, cancellationToken);
            }
        }
    }

    private static async Task<Dictionary<ChildDefinition, ILookup<object?, EntityRow>>> LoadChildrenAsync(
        IEntitySession session,
        EntityDefinition entity,
        IReadOnlyList<ChildDefinition> children,
        IReadOnlyList<EntityRow> parents,
        CancellationToken cancellationToken)
    {
        var loaded = new Dictionary<ChildDefinition, ILookup<object?, EntityRow>>();
        if (children.Count == 0 || parents.Count == 0)
        {
            return loaded;
        }

        // One query per collection for the whole page, not one per document.
        var parentKeys = parents.Select(parent => parent[entity.Key[0].Column]).ToList();

        foreach (var child in children)
        {
            var rows = await session.QueryChildrenAsync(child, parentKeys, cancellationToken);
            loaded[child] = rows.ToLookup(row => row[child.ParentKeyColumn]);
        }

        return loaded;
    }

    private static JsonObject ToJson(
        EntityDefinition entity,
        IEnumerable<FieldDefinition> fields,
        IReadOnlyList<ChildDefinition> children,
        EntityRow row,
        Dictionary<ChildDefinition, ILookup<object?, EntityRow>> loaded)
    {
        var json = EntitySerializer.ToJson(fields, row);

        foreach (var child in children)
        {
            json[child.Name] = EntitySerializer.ToJsonArray(child, loaded[child][row[entity.Key[0].Column]]);
        }

        return json;
    }
}
