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

/// <summary>Port to the database, implemented in Infrastructure.</summary>
public interface IEntityStore
{
    /// <summary>Starts one transaction. One API request uses exactly one session.</summary>
    Task<IEntitySession> OpenSessionAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Everything one request does to the database. Nothing is kept unless CommitAsync is called,
/// so a document, its stock and the partner balance are saved together or not at all.
/// </summary>
public interface IEntitySession : IAsyncDisposable
{
    Task<IReadOnlyList<EntityRow>> QueryAsync(EntityDefinition entity, EntityQuery query, CancellationToken cancellationToken);

    Task<EntityRow?> FindAsync(
        EntityDefinition entity,
        IReadOnlyList<FieldValue> key,
        IReadOnlyList<FieldDefinition> columns,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads every column and locks the row until the end of the transaction,
    /// so two requests cannot both read a stock level and both act on it.
    /// </summary>
    Task<EntityRow?> FindForUpdateAsync(
        EntityDefinition entity,
        IReadOnlyList<FieldValue> key,
        CancellationToken cancellationToken);

    /// <summary>Finds several rows by key in one query, for the objects a page links to.</summary>
    Task<IReadOnlyList<EntityRow>> FindManyAsync(
        EntityDefinition entity,
        IReadOnlyList<object?> keys,
        IReadOnlyList<FieldDefinition> columns,
        CancellationToken cancellationToken);

    /// <summary>Returns the stored row, including database defaults.</summary>
    Task<EntityRow> InsertAsync(EntityDefinition entity, IReadOnlyList<FieldValue> values, CancellationToken cancellationToken);

    /// <summary>Returns false if no row has this key.</summary>
    Task<bool> UpdateAsync(
        EntityDefinition entity,
        IReadOnlyList<FieldValue> key,
        IReadOnlyList<FieldValue> values,
        CancellationToken cancellationToken);

    /// <summary>Returns false if no row has this key.</summary>
    Task<bool> DeleteAsync(EntityDefinition entity, IReadOnlyList<FieldValue> key, CancellationToken cancellationToken);

    /// <summary>Adds to a stored number in one statement, such as a partner balance.</summary>
    Task AddToNumberAsync(
        EntityDefinition entity,
        IReadOnlyList<FieldValue> key,
        FieldDefinition field,
        decimal amount,
        CancellationToken cancellationToken);

    /// <summary>Returns the rows of a collection for several parents, in row order.</summary>
    Task<IReadOnlyList<EntityRow>> QueryChildrenAsync(
        ChildDefinition child,
        IReadOnlyList<object?> parentKeys,
        CancellationToken cancellationToken);

    Task InsertChildAsync(
        ChildDefinition child,
        object? parentKey,
        IReadOnlyList<FieldValue> values,
        CancellationToken cancellationToken);

    Task UpdateChildAsync(
        ChildDefinition child,
        object? parentKey,
        object? rowKey,
        IReadOnlyList<FieldValue> values,
        CancellationToken cancellationToken);

    /// <summary>Changes every row of one parent, such as closing all lines of a document.</summary>
    Task UpdateAllChildrenAsync(
        ChildDefinition child,
        object? parentKey,
        IReadOnlyList<FieldValue> values,
        CancellationToken cancellationToken);

    /// <summary>Adds the column of a user-defined field. The rules have already validated both names.</summary>
    Task AddColumnAsync(string table, FieldDefinition column, CancellationToken cancellationToken);

    Task ResizeColumnAsync(string table, string column, int size, CancellationToken cancellationToken);

    Task DropColumnAsync(string table, string column, CancellationToken cancellationToken);

    Task CommitAsync(CancellationToken cancellationToken);
}
