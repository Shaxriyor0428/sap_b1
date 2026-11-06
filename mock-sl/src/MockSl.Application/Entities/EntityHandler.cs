using System.Text.Json;
using System.Text.Json.Nodes;
using MockSl.Application.OData;
using MockSl.Domain.Errors;
using MockSl.Domain.Metadata;

namespace MockSl.Application.Entities;

public sealed record EntityPage(JsonArray Value, NextPage? Next);

public sealed record CreatedEntity(JsonObject Value, string KeyInUrl);

/// <summary>
/// The one generic handler: list, get, create, update, delete for any object.
/// Everything object-specific comes from its EntityDefinition.
/// </summary>
public sealed class EntityHandler(IEntityStore store)
{
    public async Task<EntityPage> ListAsync(
        EntityDefinition entity,
        ODataQuery query,
        PagePlan plan,
        CancellationToken cancellationToken)
    {
        var entityQuery = new EntityQuery(query.Select, query.Filter, query.OrderBy, plan.FetchLimit, plan.Skip);
        var rows = await store.QueryAsync(entity, entityQuery, cancellationToken);

        var visibleRows = plan.RowLimit is { } limit ? rows.Take(limit) : rows;
        var value = new JsonArray();
        foreach (var row in visibleRows)
        {
            value.Add(EntitySerializer.ToJson(query.Select, row));
        }

        return new EntityPage(value, plan.NextPageAfter(rows.Count));
    }

    public async Task<JsonObject> GetAsync(
        EntityDefinition entity,
        IReadOnlyList<FieldValue> key,
        IReadOnlyList<FieldDefinition> select,
        CancellationToken cancellationToken)
    {
        var row = await store.FindAsync(entity, key, select, cancellationToken)
            ?? throw SapErrors.EntityNotFound();

        return EntitySerializer.ToJson(select, row);
    }

    public async Task<CreatedEntity> CreateAsync(
        EntityDefinition entity,
        JsonElement body,
        CancellationToken cancellationToken)
    {
        var values = EntityBodyReader.ReadForCreate(entity, body);
        var row = await store.InsertAsync(entity, values, cancellationToken);

        return new CreatedEntity(EntitySerializer.ToJson(entity.Fields, row), EntityKey.Format(entity, row));
    }

    public async Task UpdateAsync(
        EntityDefinition entity,
        IReadOnlyList<FieldValue> key,
        JsonElement body,
        CancellationToken cancellationToken)
    {
        var values = EntityBodyReader.ReadForUpdate(entity, body);

        // A body with only ignored properties changes nothing, but the object must still exist.
        var found = values.Count > 0
            ? await store.UpdateAsync(entity, key, values, cancellationToken)
            : await store.FindAsync(entity, key, entity.Key, cancellationToken) is not null;

        if (!found)
        {
            throw SapErrors.EntityNotFound();
        }
    }

    public async Task DeleteAsync(
        EntityDefinition entity,
        IReadOnlyList<FieldValue> key,
        CancellationToken cancellationToken)
    {
        if (!await store.DeleteAsync(entity, key, cancellationToken))
        {
            throw SapErrors.EntityNotFound();
        }
    }
}
