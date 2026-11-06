using System.Data;
using Dapper;
using MockSl.Application.Entities;
using MockSl.Domain.Errors;
using MockSl.Domain.Metadata;
using Npgsql;

namespace MockSl.Infrastructure.Persistence;

/// <summary>One connection and one transaction. Disposing without a commit rolls everything back.</summary>
public sealed class PostgresEntitySession(NpgsqlConnection connection, NpgsqlTransaction transaction) : IEntitySession
{
    public async Task<IReadOnlyList<EntityRow>> QueryAsync(
        EntityDefinition entity,
        EntityQuery query,
        CancellationToken cancellationToken) =>
        await ReadRowsAsync(EntitySqlBuilder.Select(entity, query), cancellationToken);

    public Task<EntityRow?> FindAsync(
        EntityDefinition entity,
        IReadOnlyList<FieldValue> key,
        IReadOnlyList<FieldDefinition> columns,
        CancellationToken cancellationToken) =>
        ReadSingleOrNullAsync(EntitySqlBuilder.FindByKey(entity, key, columns), cancellationToken);

    public Task<EntityRow?> FindForUpdateAsync(
        EntityDefinition entity,
        IReadOnlyList<FieldValue> key,
        CancellationToken cancellationToken) =>
        ReadSingleOrNullAsync(EntitySqlBuilder.FindForUpdate(entity, key), cancellationToken);

    public async Task<EntityRow> InsertAsync(
        EntityDefinition entity,
        IReadOnlyList<FieldValue> values,
        CancellationToken cancellationToken)
    {
        try
        {
            object row = await connection.QuerySingleAsync(ToCommand(EntitySqlBuilder.Insert(entity, values), cancellationToken));
            return ToRow(row);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw SapErrors.AlreadyExists(entity.DuplicateKeyMessage(EntityKey.Describe(values, entity)));
        }
        catch (PostgresException exception) when (ToBadDataError(exception) is { } error)
        {
            throw error;
        }
    }

    public Task<bool> UpdateAsync(
        EntityDefinition entity,
        IReadOnlyList<FieldValue> key,
        IReadOnlyList<FieldValue> values,
        CancellationToken cancellationToken) =>
        ExecuteWriteAsync(EntitySqlBuilder.Update(entity, key, values), cancellationToken);

    public async Task<bool> DeleteAsync(
        EntityDefinition entity,
        IReadOnlyList<FieldValue> key,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ExecuteOnRowAsync(EntitySqlBuilder.Delete(entity, key), cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            throw SapErrors.ObjectInUse();
        }
    }

    public Task AddToNumberAsync(
        EntityDefinition entity,
        IReadOnlyList<FieldValue> key,
        FieldDefinition field,
        decimal amount,
        CancellationToken cancellationToken) =>
        ExecuteWriteAsync(EntitySqlBuilder.AddToNumber(entity, key, field, amount), cancellationToken);

    public async Task<IReadOnlyList<EntityRow>> QueryChildrenAsync(
        ChildDefinition child,
        IReadOnlyList<object?> parentKeys,
        CancellationToken cancellationToken) =>
        parentKeys.Count == 0 ? [] : await ReadRowsAsync(ChildSqlBuilder.Select(child, parentKeys), cancellationToken);

    public Task InsertChildAsync(
        ChildDefinition child,
        object? parentKey,
        IReadOnlyList<FieldValue> values,
        CancellationToken cancellationToken) =>
        ExecuteWriteAsync(ChildSqlBuilder.Insert(child, parentKey, values), cancellationToken);

    public Task UpdateChildAsync(
        ChildDefinition child,
        object? parentKey,
        object? rowKey,
        IReadOnlyList<FieldValue> values,
        CancellationToken cancellationToken) =>
        ExecuteWriteAsync(ChildSqlBuilder.Update(child, parentKey, rowKey, values), cancellationToken);

    public Task UpdateAllChildrenAsync(
        ChildDefinition child,
        object? parentKey,
        IReadOnlyList<FieldValue> values,
        CancellationToken cancellationToken) =>
        ExecuteWriteAsync(ChildSqlBuilder.UpdateAll(child, parentKey, values), cancellationToken);

    public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        await transaction.DisposeAsync();
        await connection.DisposeAsync();
    }

    private async Task<List<EntityRow>> ReadRowsAsync(SqlStatement statement, CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync(ToCommand(statement, cancellationToken));
        return rows.Cast<object>().Select(ToRow).ToList();
    }

    private async Task<EntityRow?> ReadSingleOrNullAsync(SqlStatement statement, CancellationToken cancellationToken)
    {
        object? row = await connection.QuerySingleOrDefaultAsync(ToCommand(statement, cancellationToken));
        return row is null ? null : ToRow(row);
    }

    private async Task<bool> ExecuteOnRowAsync(SqlStatement statement, CancellationToken cancellationToken)
    {
        var affected = await connection.ExecuteAsync(ToCommand(statement, cancellationToken));
        return affected > 0;
    }

    private async Task<bool> ExecuteWriteAsync(SqlStatement statement, CancellationToken cancellationToken)
    {
        try
        {
            return await ExecuteOnRowAsync(statement, cancellationToken);
        }
        catch (PostgresException exception) when (ToBadDataError(exception) is { } error)
        {
            throw error;
        }
    }

    // The last line of defence: bad data the client sent must come back as a SAP error, not as a 500.
    private static ServiceLayerException? ToBadDataError(PostgresException exception) => exception.SqlState switch
    {
        PostgresErrorCodes.ForeignKeyViolation => SapErrors.BrokenReference(),
        PostgresErrorCodes.NumericValueOutOfRange => SapErrors.NumberOutOfRange(),
        _ => null,
    };

    private CommandDefinition ToCommand(SqlStatement statement, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();

        foreach (var parameter in statement.Parameters)
        {
            if (parameter.Value is DateOnly date)
            {
                parameters.Add(parameter.Name, date.ToDateTime(TimeOnly.MinValue), DbType.Date);
            }
            else
            {
                parameters.Add(parameter.Name, parameter.Value);
            }
        }

        return new CommandDefinition(statement.Text, parameters, transaction, cancellationToken: cancellationToken);
    }

    // Dapper returns untyped rows that can be read as dictionaries keyed by column name.
    private static EntityRow ToRow(object row) =>
        new(((IDictionary<string, object>)row).ToDictionary(column => column.Key, column => (object?)column.Value));
}
