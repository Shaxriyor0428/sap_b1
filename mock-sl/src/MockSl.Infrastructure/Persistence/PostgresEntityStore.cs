using MockSl.Application.Entities;
using Npgsql;

namespace MockSl.Infrastructure.Persistence;

public sealed class PostgresEntityStore(NpgsqlDataSource dataSource) : IEntityStore
{
    public async Task<IEntitySession> OpenSessionAsync(CancellationToken cancellationToken)
    {
        var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var transaction = await connection.BeginTransactionAsync(cancellationToken);
        return new PostgresEntitySession(connection, transaction);
    }
}
