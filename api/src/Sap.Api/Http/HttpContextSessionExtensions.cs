using Sap.Application.Auth;

namespace Sap.Api.Http;

/// <summary>Keeps the current user's session on the request, so the controller can pass it on.</summary>
public static class HttpContextSessionExtensions
{
    private const string SessionKey = "UserSession";

    public static void SetUserSession(this HttpContext context, UserSession session) =>
        context.Items[SessionKey] = session;

    public static UserSession? FindUserSession(this HttpContext context) =>
        context.Items.TryGetValue(SessionKey, out var session) ? (UserSession?)session : null;

    /// <summary>For actions behind [RequireSession], where a missing session would be a bug.</summary>
    public static UserSession GetUserSession(this HttpContext context) =>
        context.FindUserSession() ?? throw new InvalidOperationException("The action needs [RequireSession].");
}
