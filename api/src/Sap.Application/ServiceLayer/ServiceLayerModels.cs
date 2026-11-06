namespace Sap.Application.ServiceLayer;

/// <summary>The cookies that identify one user's Service Layer session (guide p.17).</summary>
public sealed record SlCookies(string B1Session, string? RouteId);

public sealed record SlLogin(SlCookies Cookies, int SessionTimeoutMinutes);

/// <summary>
/// A business partner as the Service Layer returns it. Property names match the JSON exactly.
/// The real object has hundreds of fields; only these are read, the rest are ignored.
/// </summary>
public sealed record BusinessPartner(
    string CardCode,
    string? CardName,
    string? CardType,
    string? Phone1,
    string? Cellular,
    string? EmailAddress,
    string? Address,
    string? City,
    decimal? CurrentAccountBalance);

/// <summary>A body for POST or PATCH. Null properties are not sent, so PATCH leaves those fields unchanged.</summary>
public sealed record BusinessPartnerWrite
{
    public string? CardCode { get; init; }

    public string? CardName { get; init; }

    public string? CardType { get; init; }

    public string? Phone1 { get; init; }

    public string? Cellular { get; init; }

    public string? EmailAddress { get; init; }

    public string? Address { get; init; }

    public string? City { get; init; }
}

public sealed record BusinessPartnerQuery(string Filter, string OrderBy, int Top, long Skip);
