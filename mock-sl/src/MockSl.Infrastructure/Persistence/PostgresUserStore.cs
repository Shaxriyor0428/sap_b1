using Dapper;
using MockSl.Application.Auth;
using Npgsql;

namespace MockSl.Infrastructure.Persistence;

public sealed class PostgresUserStore(NpgsqlDataSource dataSource) : IUserStore
{
    // pgcrypto compares the password with the stored bcrypt hash, see db/900_seed_users.sql.
    private const string FindUserSql = """
        SELECT "USER_CODE" AS "UserCode", "SUPERUSER" AS "SuperUser"
        FROM "OUSR"
        WHERE "USER_CODE" = @userName
          AND "PASSWORD" = crypt(@password, "PASSWORD")
          AND "Locked" = 'N'
        """;

    public async Task<UserAccount?> FindByCredentialsAsync(
        string userName,
        string password,
        CancellationToken cancellationToken)
    {
        var command = new CommandDefinition(
            FindUserSql,
            new { userName, password },
            cancellationToken: cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<UserRow>(command);

        return row is null ? null : new UserAccount(row.UserCode, row.SuperUser == "Y");
    }
}

internal sealed record UserRow(string UserCode, string SuperUser);
