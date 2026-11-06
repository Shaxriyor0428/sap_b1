using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sap.Application.Errors;
using Sap.Application.ServiceLayer;

namespace Sap.Infrastructure.ServiceLayer;

/// <summary>
/// The only class that talks to the Service Layer. It speaks plain HTTP,
/// so switching from the mock to real SAP only changes the settings.
/// </summary>
public sealed class ServiceLayerClient(HttpClient http, ServiceLayerOptions options) : IServiceLayerGateway
{
    private const string RouteIdCookie = "ROUTEID";

    // Property names stay as written: the Service Layer's JSON uses CardCode, not cardCode.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<SlLogin> LoginAsync(string userName, string password, CancellationToken cancellationToken)
    {
        using var request = NewRequest(HttpMethod.Post, "Login", cookies: null);
        request.Content = ToJsonContent(new LoginRequest(options.CompanyDb, userName, password));

        using var response = await SendAsync(request, cancellationToken);

        // A 401 here means wrong credentials, not an expired session, so its text goes to the user.
        if (!response.IsSuccessStatusCode)
        {
            throw await ReadErrorAsync(response, cancellationToken);
        }

        var body = await ReadJsonAsync<LoginResponse>(response, cancellationToken);
        return new SlLogin(new SlCookies(body.SessionId, ReadCookie(response, RouteIdCookie)), body.SessionTimeout);
    }

    public async Task LogoutAsync(SlCookies cookies, CancellationToken cancellationToken)
    {
        using var request = NewRequest(HttpMethod.Post, "Logout", cookies);
        using var response = await SendWithSessionAsync(request, cancellationToken);
    }

    public async Task<IReadOnlyList<BusinessPartner>> ListBusinessPartnersAsync(
        SlCookies cookies,
        BusinessPartnerQuery query,
        CancellationToken cancellationToken)
    {
        // Only the values are escaped. SAP reports that an escaped "$" in the option name fails.
        var uri = "BusinessPartners"
            + $"?$filter={Uri.EscapeDataString(query.Filter)}"
            + $"&$orderby={Uri.EscapeDataString(query.OrderBy)}"
            + $"&$top={query.Top}"
            + $"&$skip={query.Skip}";

        using var request = NewRequest(HttpMethod.Get, uri, cookies);

        // The default page is 20 rows (guide p.35), which would cut off a larger $top.
        request.Headers.TryAddWithoutValidation("Prefer", $"odata.maxpagesize={query.Top}");

        using var response = await SendWithSessionAsync(request, cancellationToken);
        var body = await ReadJsonAsync<ListResponse>(response, cancellationToken);
        return body.Value;
    }

    public async Task<string?> GetCardTypeAsync(SlCookies cookies, string cardCode, CancellationToken cancellationToken)
    {
        using var request = NewRequest(HttpMethod.Get, $"{PartnerPath(cardCode)}?$select=CardCode,CardType", cookies);
        using var response = await SendWithSessionAsync(request, cancellationToken);
        var body = await ReadJsonAsync<CardTypeResponse>(response, cancellationToken);
        return body.CardType;
    }

    public async Task<BusinessPartner> CreateBusinessPartnerAsync(
        SlCookies cookies,
        BusinessPartnerWrite partner,
        CancellationToken cancellationToken)
    {
        using var request = NewRequest(HttpMethod.Post, "BusinessPartners", cookies);
        request.Content = ToJsonContent(partner);

        using var response = await SendWithSessionAsync(request, cancellationToken);
        return await ReadJsonAsync<BusinessPartner>(response, cancellationToken);
    }

    public async Task UpdateBusinessPartnerAsync(
        SlCookies cookies,
        string cardCode,
        BusinessPartnerWrite changes,
        CancellationToken cancellationToken)
    {
        using var request = NewRequest(HttpMethod.Patch, PartnerPath(cardCode), cookies);
        request.Content = ToJsonContent(changes);

        using var response = await SendWithSessionAsync(request, cancellationToken);
    }

    public async Task DeleteBusinessPartnerAsync(SlCookies cookies, string cardCode, CancellationToken cancellationToken)
    {
        using var request = NewRequest(HttpMethod.Delete, PartnerPath(cardCode), cookies);
        using var response = await SendWithSessionAsync(request, cancellationToken);
    }

    // A quote is doubled by OData rules. Escaping then keeps characters such as "/" or "?"
    // inside the key, so a code cannot change which URL is called.
    private static string PartnerPath(string cardCode) =>
        $"BusinessPartners('{Uri.EscapeDataString(cardCode.Replace("'", "''"))}')";

    private static HttpRequestMessage NewRequest(HttpMethod method, string uri, SlCookies? cookies)
    {
        var request = new HttpRequestMessage(method, uri);

        if (cookies is not null)
        {
            var header = cookies.RouteId is null
                ? $"B1SESSION={cookies.B1Session}"
                : $"B1SESSION={cookies.B1Session}; {RouteIdCookie}={cookies.RouteId}";

            request.Headers.TryAddWithoutValidation("Cookie", header);
        }

        return request;
    }

    // StringContent sets Content-Length. The streaming JSON helpers would send a chunked body instead.
    private static StringContent ToJsonContent<T>(T value) =>
        new(JsonSerializer.Serialize(value, JsonOptions), Encoding.UTF8, "application/json");

    private async Task<HttpResponseMessage> SendWithSessionAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            throw new SessionExpiredException();
        }

        if (!response.IsSuccessStatusCode)
        {
            using (response)
            {
                throw await ReadErrorAsync(response, cancellationToken);
            }
        }

        return response;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new ServiceLayerUnavailableException(exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Cancelled without the caller asking for it: HttpClient hit its timeout.
            throw new ServiceLayerUnavailableException(exception);
        }
    }

    private static async Task<ServiceLayerErrorException> ReadErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return ServiceLayerErrorParser.Parse((int)response.StatusCode, body);
    }

    private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken) =>
        await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
        ?? throw new JsonException($"The Service Layer returned an empty {typeof(T).Name}.");

    private static string? ReadCookie(HttpResponseMessage response, string name)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            return null;
        }

        var prefix = $"{name}=";
        return cookies
            .Select(cookie => cookie.Split(';')[0].Trim())
            .Where(pair => pair.StartsWith(prefix, StringComparison.Ordinal))
            .Select(pair => pair[prefix.Length..])
            .FirstOrDefault();
    }

    private sealed record LoginRequest(string CompanyDB, string UserName, string Password);

    private sealed record LoginResponse(string SessionId, int SessionTimeout);

    private sealed record CardTypeResponse(string? CardType);

    private sealed record ListResponse([property: JsonPropertyName("value")] IReadOnlyList<BusinessPartner> Value);
}
