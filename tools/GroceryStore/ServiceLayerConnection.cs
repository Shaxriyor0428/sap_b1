using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Sap.Application.Errors;
using Sap.Application.ServiceLayer;
using Sap.Infrastructure.ServiceLayer;

namespace GroceryStore;

/// <summary>
/// The script's way to the Service Layer: HTTP only, never the database, as the roadmap requires.
/// Login, TLS and error reading are the same code the backend uses.
/// </summary>
public sealed class ServiceLayerConnection : IDisposable
{
    private readonly HttpClient _http;
    private readonly ServiceLayerClient _client;
    private SlCookies? _cookies;

    public ServiceLayerConnection(GrocerySettings settings)
    {
        var options = ServiceLayerOptions.Create(settings.BaseUrl, settings.CompanyDb, settings.SkipTlsVerify);
        _http = new HttpClient(ServiceLayerHttpHandler.Create(options.SkipTlsVerify)) { BaseAddress = options.BaseAddress };
        _client = new ServiceLayerClient(_http, options);
    }

    public async Task LoginAsync(string userName, string password, CancellationToken cancellationToken)
    {
        var login = await _client.LoginAsync(userName, password, cancellationToken);
        _cookies = login.Cookies;
    }

    public Task LogoutAsync(CancellationToken cancellationToken) =>
        _cookies is null ? Task.CompletedTask : _client.LogoutAsync(_cookies, cancellationToken);

    /// <summary>Creates an object and returns what the Service Layer stored, including computed values.</summary>
    public async Task<JsonElement> CreateAsync(string resource, object body, CancellationToken cancellationToken)
    {
        using var request = NewRequest(HttpMethod.Post, resource);
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var response = await SendAsync(request, cancellationToken);
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    public async Task<JsonElement> GetAsync(string resource, CancellationToken cancellationToken)
    {
        using var request = NewRequest(HttpMethod.Get, resource);
        using var response = await SendAsync(request, cancellationToken);
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    /// <summary>Runs a bound action such as Invoices(1)/Cancel.</summary>
    public async Task RunActionAsync(string resource, CancellationToken cancellationToken)
    {
        using var request = NewRequest(HttpMethod.Post, resource);
        using var response = await SendAsync(request, cancellationToken);
    }

    public void Dispose() => _http.Dispose();

    private HttpRequestMessage NewRequest(HttpMethod method, string resource)
    {
        var request = new HttpRequestMessage(method, resource);
        var cookies = _cookies ?? throw new InvalidOperationException("Log in first.");

        request.Headers.TryAddWithoutValidation(
            "Cookie",
            cookies.RouteId is null
                ? $"B1SESSION={cookies.B1Session}"
                : $"B1SESSION={cookies.B1Session}; ROUTEID={cookies.RouteId}");

        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;

        try
        {
            response = await _http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new ServiceLayerUnavailableException(exception);
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw ServiceLayerErrorParser.Parse((int)response.StatusCode, body);
        }
    }
}
