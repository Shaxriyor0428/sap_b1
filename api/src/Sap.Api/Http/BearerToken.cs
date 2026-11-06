namespace Sap.Api.Http;

public static class BearerToken
{
    private const string Prefix = "Bearer ";

    /// <summary>Returns the token from an Authorization header, or null if there is none.</summary>
    public static string? Read(string? header)
    {
        if (header is null || !header.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = header[Prefix.Length..].Trim();
        return token.Length == 0 ? null : token;
    }
}
