using Microsoft.Extensions.DependencyInjection;
using Sap.Application.Auth;
using Sap.Application.ServiceLayer;
using Sap.Infrastructure.Auth;
using Sap.Infrastructure.ServiceLayer;

namespace Sap.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Connects the ports declared in Application to their implementations.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, ServiceLayerOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<ISessionStore, InMemorySessionStore>();

        services
            .AddHttpClient<IServiceLayerGateway, ServiceLayerClient>(client => client.BaseAddress = options.BaseAddress)
            .ConfigurePrimaryHttpMessageHandler(() => ServiceLayerHttpHandler.Create(options.SkipTlsVerify));

        return services;
    }
}
