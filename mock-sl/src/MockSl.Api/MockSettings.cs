using System.Globalization;

namespace MockSl.Api;

/// <summary>Settings read from environment variables, see .env.example.</summary>
public sealed record MockSettings(string ConnectionString, string CompanyDb, int SessionTimeoutMinutes)
{
    public static MockSettings From(IConfiguration configuration) => new(
        Required(configuration, "MOCK_DB_CONNECTION"),
        Required(configuration, "MOCK_COMPANY_DB"),
        int.Parse(Required(configuration, "MOCK_SESSION_TIMEOUT_MINUTES"), CultureInfo.InvariantCulture));

    private static string Required(IConfiguration configuration, string name) =>
        configuration[name] ?? throw new InvalidOperationException($"Setting {name} is missing.");
}
