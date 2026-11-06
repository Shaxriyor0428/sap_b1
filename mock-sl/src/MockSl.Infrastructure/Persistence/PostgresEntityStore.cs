using System.Data;
using Dapper;
using MockSl.Application.Entities;
using MockSl.Domain.Errors;
using MockSl.Domain.Metadata;
using Npgsql;

namespace MockSl.Infrastructure.Persistence;

public sealed class PostgresEntityStore(NpgsqlDataSource dataSource) : IEntityStore
{
    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(
        EntityDefinition entity,
        EntityQuery query,
        CancellationToken cancellationToken)
    {
        var command = ToCommand(EntitySqlBuilder.Select(entity, query), cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync(command);
        return rows.Cast<object>().Select(ToRow).ToList();
    }

    public async Task<IReadOnlyDictionary<string, object?>?> FindAsync(
        EntityDefinition entity,
        IReadOnlyList<FieldValue> key,
        IReadOnlyList<FieldDefinition> columns,
        CancellationToken cancellationToken)
    {
        var command = ToCommand(EntitySqlBuilder.FindByKey(entity, key, columns), cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        object? row = await connection.QuerySingleOrDefaultAsync(command);
        return row is null ? null : ToRow(row);
    }

    public async Task<IReadOnlyDictionary<string, object?>> InsertAsync(
        EntityDefinition entity,
        IReadOnlyList<FieldValue> values,
        CancellationToken cancellationToken)
    {
        var command = ToCommand(EntitySqlBuilder.Insert(entity, values), cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        try
        {
            object row = await connection.QuerySingleAsync(command);
            return ToRow(row);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw SapErrors.AlreadyExists(entity.DuplicateKeyMessage(EntityKey.Describe(values, entity)));
        }
    }

    public Task<bool> UpdateAsync(
        EntityDefinition entity,
        IReadOnlyList<FieldValue> key,
        IReadOnlyList<FieldValue> values,
        CancellationToken cancellationToken) =>
        ExecuteOnRowAsync(EntitySqlBuilder.Update(entity, key, values), cancellationToken);

    public Task<bool> DeleteAsync(
        EntityDefinition entity,
        IReadOnlyList<FieldValue> key,
        CancellationToken cancellationToken) =>
        ExecuteOnRowAsync(EntitySqlBuilder.Delete(entity, key), cancellationToken);

    private async Task<bool> ExecuteOnRowAsync(SqlStatement statement, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(ToCommand(statement, cancellationToken));
        return affected > 0;
    }

    private static CommandDefinition ToCommand(SqlStatement statement, CancellationToken cancellationToken)
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

        return new CommandDefinition(statement.Text, parameters, cancellationToken: cancellationToken);
    }

    // Dapper returns untyped rows that can be read as dictionaries keyed by column name.
    private static Dictionary<string, object?> ToRow(object row) =>
        ((IDictionary<string, object>)row).ToDictionary(column => column.Key, column => (object?)column.Value);
}
