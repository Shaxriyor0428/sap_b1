using System.Text.Json;
using System.Text.Json.Nodes;
using MockSl.Api.Http;
using MockSl.Application.Auth;

namespace MockSl.Api.Endpoints;

public static class AuthEndpoints
{
    // Sample values from the guide, p.16.
    private const string ServerVersion = "1000110";
    private const string RouteIdCookie = "ROUTEID=.node1; path=/b1s";

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost($"{SlJson.ApiRoot}/Login", LoginAsync);
        app.MapPost($"{SlJson.ApiRoot}/Logout", Logout).AddEndpointFilter<SessionFilter>();
    }

    private static async Task<IResult> LoginAsync(HttpContext http, AuthService auth, CancellationToken cancellationToken)
    {
        using var document = await SlJson.ReadBodyAsync(http.Request, cancellationToken);
        var body = document.RootElement;

        var session = await auth.LoginAsync(
            ReadText(body, "CompanyDB"),
            ReadText(body, "UserName"),
            ReadText(body, "Password"),
            cancellationToken);

        http.Response.Headers.Append("Set-Cookie", $"{SessionFilter.SessionCookie}={session.Id};HttpOnly;");
        http.Response.Headers.Append("Set-Cookie", RouteIdCookie);

        return SlJson.Result(new JsonObject
        {
            ["@odata.context"] = $"{SlJson.RootUrl(http.Request)}/$metadata#B1Sessions/$entity",
            ["SessionId"] = session.Id,
            ["Version"] = ServerVersion,
            ["SessionTimeout"] = auth.SessionTimeoutMinutes,
        });
    }

    private static IResult Logout(HttpContext http, AuthService auth)
    {
        auth.Logout(http.Request.Cookies[SessionFilter.SessionCookie]!);
        return Results.NoContent();
    }

    private static string? ReadText(JsonElement body, string name) =>
        body.ValueKind == JsonValueKind.Object
        && body.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
