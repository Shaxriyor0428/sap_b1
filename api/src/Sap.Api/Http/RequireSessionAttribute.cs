using Microsoft.AspNetCore.Mvc.Filters;
using Sap.Application.Auth;

namespace Sap.Api.Http;

/// <summary>
/// Put on a controller or action that needs a logged-in user. It reads "Authorization: Bearer token",
/// finds the session and keeps it for the action. Without a live session the request ends in a 401.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireSessionAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var http = context.HttpContext;
        var auth = http.RequestServices.GetRequiredService<AuthService>();

        var session = auth.RequireSession(BearerToken.Read(http.Request.Headers.Authorization));
        http.SetUserSession(session);
    }
}
