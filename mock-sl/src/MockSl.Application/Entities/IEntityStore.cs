using MockSl.Application.OData;
using MockSl.Domain.Metadata;

namespace MockSl.Application.Entities;

public sealed record FieldValue(FieldDefinition Field, object? Value);

public sealed record EntityQuery(
    IReadOnlyList<FieldDefinition> Columns,
    FilterNode? Filter,
    IReadOnlyList<OrderByItem> OrderBy,
    int? Limit,
    int Offset);

/// <summary>
/// Port to the database, implemented in Infrastructure.
/// A row is a dictionary keyed by database column name.
/// </summary>
public interface IEntityStore
{
    Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(
        EntityDefinition entity,
        EntityQuery query,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, object?>?> FindAsync(
        EntityDefinition entity,
        IReadOnlyList<FieldValue> key,
        IReadOnlyList<FieldDefinition> columns,
        CancellationToken cancellationToken);

    /// <summary>Returns the stored row, including database defaults.</summary>
    Task<IReadOnlyDictionary<string, object?>> InsertAsync(
        EntityDefinition entity,
        IReadOnlyList<FieldValue> values,
        CancellationToken cancellationToken);

    /// <summary>Returns false if no row has this key.</summary>
    Task<bool> UpdateAsync(
        EntityDefinition entity,
        IReadOnlyList<FieldValue> key,
        IReadOnlyList<FieldValue> values,
        CancellationToken cancellationToken);

    /// <summary>Returns false if no row has this key.</summary>
    Task<bool> DeleteAsync(
        EntityDefinition entity,
        IReadOnlyList<FieldValue> key,
        CancellationToken cancellationToken);
}
