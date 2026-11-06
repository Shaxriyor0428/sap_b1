using Microsoft.AspNetCore.Mvc;
using Sap.Api.Http;
using Sap.Application.Auth;

namespace Sap.Api.Controllers;

public sealed record LoginRequest(string UserName, string Password);

public sealed record LoginResponse(string Token, int ExpiresInMinutes);

[ApiController]
[Route("api")]
public sealed class AuthController(AuthService auth) : ControllerBase
{
    [HttpPost("login")]
    public async Task<LoginResponse> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var session = await auth.LoginAsync(request.UserName, request.Password, cancellationToken);
        return new LoginResponse(session.Token, (int)session.Timeout.TotalMinutes);
    }

    [HttpPost("logout")]
    [RequireSession]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await auth.LogoutAsync(HttpContext.GetUserSession(), cancellationToken);
        return NoContent();
    }
}
