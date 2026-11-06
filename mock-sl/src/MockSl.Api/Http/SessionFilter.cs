using MockSl.Application.Auth;

namespace MockSl.Api.Http;

/// <summary>Runs before every endpoint except Login and rejects requests without a live session.</summary>
public sealed class SessionFilter(AuthService auth) : IEndpointFilter
{
    public const string SessionCookie = "B1SESSION";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        auth.RequireSession(context.HttpContext.Request.Cookies[SessionCookie]);
        return await next(context);
    }
}
