using MockSl.Application.Entities;
using MockSl.Application.SqlQueries;
using MockSl.Domain.Errors;
using Npgsql;
using NpgsqlTypes;

namespace MockSl.Infrastructure.Persistence;

public sealed class PostgresEntityStore(NpgsqlDataSource dataSource) : IEntityStore
{
    private const string DataExceptionClass = "22";
    private const string SyntaxOrNameErrorClass = "42";

    public async Task<IEntitySession> OpenSessionAsync(CancellationToken cancellationToken)
    {
        var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var transaction = await connection.BeginTransactionAsync(cancellationToken);
        return new PostgresEntitySession(connection, transaction);
    }

    public async Task<IReadOnlyList<SqlRow>> RunReadOnlyQueryAsync(SqlRun run, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // The last line of defence: whatever got past the checks, the database refuses to write.
        await using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, transaction))
        {
            await readOnly.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var command = new NpgsqlCommand(SqlPaging.Wrap(run.Executable, run.Limit, run.Offset), connection, transaction);

        // Unknown lets PostgreSQL work out the type from where the parameter stands: a date next to
        // "DocDate", a number next to "DocTotal". A value that does not fit is an error, never SQL.
        foreach (var (name, value) in run.Parameters)
        {
            command.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Unknown) { Value = value });
        }

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await ReadRowsAsync(reader, cancellationToken);
        }
        catch (PostgresException exception) when (ToSqlError(exception, run) is { } error)
        {
            throw error;
        }
    }

    private static async Task<List<SqlRow>> ReadRowsAsync(NpgsqlDataReader reader, CancellationToken cancellationToken)
    {
        var rows = new List<SqlRow>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var cells = new List<SqlCell>();

            for (var column = 0; column < reader.FieldCount; column++)
            {
                var value = await reader.IsDBNullAsync(column, cancellationToken) ? null : reader.GetValue(column);
                cells.Add(new SqlCell(reader.GetName(column), value));
            }

            rows.Add(new SqlRow(cells));
        }

        return rows;
    }

    private static ServiceLayerException? ToSqlError(PostgresException exception, SqlRun run)
    {
        var errorClass = exception.SqlState[..2];

        return errorClass switch
        {
            DataExceptionClass when run.Parameters.Count > 0 => SapErrors.SqlParameterError(),
            DataExceptionClass or SyntaxOrNameErrorClass => SapErrors.SqlCannotRun(exception.MessageText),
            _ => null,
        };
    }
}
