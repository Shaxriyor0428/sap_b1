using Sap.Infrastructure.ServiceLayer;

namespace Sap.Api;

/// <summary>Settings read from environment variables, see .env.example.</summary>
public static class ApiSettings
{
    public static ServiceLayerOptions ReadServiceLayerOptions(IConfiguration configuration) =>
        ServiceLayerOptions.Create(
            Required(configuration, "SL_BASE_URL"),
            Required(configuration, "SL_COMPANY_DB"),
            string.Equals(configuration["SL_SKIP_TLS_VERIFY"], "true", StringComparison.OrdinalIgnoreCase));

    private static string Required(IConfiguration configuration, string name) =>
        configuration[name] ?? throw new InvalidOperationException($"Setting {name} is missing.");
}
