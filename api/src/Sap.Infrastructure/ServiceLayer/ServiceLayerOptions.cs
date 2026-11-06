namespace Sap.Infrastructure.ServiceLayer;

/// <summary>Everything that differs between the mock and real SAP. All of it comes from .env.</summary>
public sealed record ServiceLayerOptions(Uri BaseAddress, string CompanyDb, bool SkipTlsVerify)
{
    public static ServiceLayerOptions Create(string baseUrl, string companyDb, bool skipTlsVerify)
    {
        // Without the final slash, relative URLs would replace the "v2" segment instead of following it.
        var normalized = baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/";
        return new ServiceLayerOptions(new Uri(normalized), companyDb, skipTlsVerify);
    }
}
