using Microsoft.Extensions.DependencyInjection;
using MockSl.Application.Auth;
using MockSl.Application.Entities;
using MockSl.Infrastructure.Auth;
using MockSl.Infrastructure.Persistence;
using Npgsql;

namespace MockSl.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Connects the ports declared in Application to their implementations.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
        services.AddSingleton<IEntityStore, PostgresEntityStore>();
        services.AddSingleton<IUserStore, PostgresUserStore>();
        services.AddSingleton<ISessionStore, InMemorySessionStore>();
        return services;
    }
}
