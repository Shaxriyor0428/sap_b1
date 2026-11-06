using MockSl.Application.Entities;
using MockSl.Application.SqlQueries;
using MockSl.Domain.Metadata;

namespace MockSl.UnitTests.Fakes;

/// <summary>Stands in for the database: returns the rows a test gives it and records what was asked.</summary>
internal sealed class FakeEntityStore : IEntityStore, IEntitySession
{
    public List<EntityRow> Rows { get; init; } = [];

    public List<EntityRow> LinkedRows { get; init; } = [];

    public List<SqlRow> SqlRows { get; init; } = [];

    public IReadOnlyDictionary<string, IReadOnlyList<string>> TableColumns { get; init; } = new Dictionary<string, IReadOnlyList<string>>();

    public SqlRun? LastRun { get; private set; }

    public EntityRow? InsertedRow { get; init; }

    public bool UpdateFinds { get; init; } = true;

    public bool DeleteFinds { get; init; } = true;

    public EntityQuery? LastQuery { get; private set; }

    public IReadOnlyList<FieldValue> InsertedValues { get; private set; } = [];

    public IReadOnlyList<object?> LastLinkedKeys { get; private set; } = [];

    public bool UpdateCalled { get; private set; }

    public bool Committed { get; private set; }

    public bool SessionOpened { get; private set; }

    /// <summary>Every database step in order, for tests where the order is the point.</summary>
    public List<string> Steps { get; } = [];

    public Task<IEntitySession> OpenSessionAsync(CancellationToken cancellationToken)
    {
        SessionOpened = true;
        return Task.FromResult<IEntitySession>(this);
    }

    public Task<IReadOnlyList<SqlRow>> RunReadOnlyQueryAsync(SqlRun run, CancellationToken cancellationToken)
    {
        LastRun = run;
        return Task.FromResult<IReadOnlyList<SqlRow>>(SqlRows);
    }

    public Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ReadTableColumnsAsync(
        IEnumerable<string> tables,
        CancellationToken cancellationToken) =>
        Task.FromResult(TableColumns);

    public Task<IReadOnlyList<EntityRow>> QueryAsync(EntityDefinition entity, EntityQuery query, CancellationToken cancellationToken)
    {
        Steps.Add("query");
        LastQuery = query;
        return Task.FromResult<IReadOnlyList<EntityRow>>(Rows);
    }

    public Task<EntityRow?> FindAsync(
        EntityDefinition entity,
        IReadOnlyList<FieldValue> key,
        IReadOnlyList<FieldDefinition> columns,
        CancellationToken cancellationToken) =>
        Task.FromResult(Rows.FirstOrDefault());

    public Task<EntityRow?> FindForUpdateAsync(EntityDefinition entity, IReadOnlyList<FieldValue> key, CancellationToken cancellationToken) =>
        Task.FromResult(Rows.FirstOrDefault());

    public Task<IReadOnlyList<EntityRow>> FindManyAsync(
        EntityDefinition entity,
        IReadOnlyList<object?> keys,
        IReadOnlyList<FieldDefinition> columns,
        CancellationToken cancellationToken)
    {
        Steps.Add("find many");
        LastLinkedKeys = keys;
        return Task.FromResult<IReadOnlyList<EntityRow>>(LinkedRows);
    }

    public Task<EntityRow> InsertAsync(EntityDefinition entity, IReadOnlyList<FieldValue> values, CancellationToken cancellationToken)
    {
        Steps.Add("insert");
        InsertedValues = values;
        return Task.FromResult(InsertedRow!);
    }

    public Task<bool> UpdateAsync(
        EntityDefinition entity,
        IReadOnlyList<FieldValue> key,
        IReadOnlyList<FieldValue> values,
        CancellationToken cancellationToken)
    {
        UpdateCalled = true;
        return Task.FromResult(UpdateFinds);
    }

    public Task<bool> DeleteAsync(EntityDefinition entity, IReadOnlyList<FieldValue> key, CancellationToken cancellationToken) =>
        Task.FromResult(DeleteFinds);

    public Task AddToNumberAsync(
        EntityDefinition entity,
        IReadOnlyList<FieldValue> key,
        FieldDefinition field,
        decimal amount,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<EntityRow>> QueryChildrenAsync(
        ChildDefinition child,
        IReadOnlyList<object?> parentKeys,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<EntityRow>>([]);

    public Task InsertChildAsync(
        ChildDefinition child,
        object? parentKey,
        IReadOnlyList<FieldValue> values,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task UpdateChildAsync(
        ChildDefinition child,
        object? parentKey,
        object? rowKey,
        IReadOnlyList<FieldValue> values,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task UpdateAllChildrenAsync(
        ChildDefinition child,
        object? parentKey,
        IReadOnlyList<FieldValue> values,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task AddColumnAsync(string table, FieldDefinition column, CancellationToken cancellationToken)
    {
        Steps.Add($"add column {table}.{column.Column}");
        return Task.CompletedTask;
    }

    public Task ResizeColumnAsync(string table, string column, int size, CancellationToken cancellationToken)
    {
        Steps.Add($"resize column {table}.{column} {size}");
        return Task.CompletedTask;
    }

    public Task DropColumnAsync(string table, string column, CancellationToken cancellationToken)
    {
        Steps.Add($"drop column {table}.{column}");
        return Task.CompletedTask;
    }

    public Task CommitAsync(CancellationToken cancellationToken)
    {
        Committed = true;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
