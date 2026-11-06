using System.Security.Cryptography;
using Sap.Application.Errors;
using Sap.Application.ServiceLayer;

namespace Sap.Application.Auth;

/// <summary>
/// Login, logout and session checks. The Service Layer decides whether the credentials are valid;
/// this class only maps its session to a token of our own.
/// </summary>
public sealed class AuthService(IServiceLayerGateway serviceLayer, ISessionStore sessions, TimeProvider clock)
{
    public async Task<UserSession> LoginAsync(string userName, string password, CancellationToken cancellationToken)
    {
        var login = await serviceLayer.LoginAsync(userName, password, cancellationToken);

        var now = clock.GetUtcNow();
        sessions.RemoveExpired(now);

        var session = new UserSession(
            NewToken(),
            userName,
            login.Cookies,
            TimeSpan.FromMinutes(login.SessionTimeoutMinutes),
            now);

        sessions.Save(session);
        return session;
    }

    public async Task LogoutAsync(UserSession session, CancellationToken cancellationToken)
    {
        sessions.Remove(session.Token);

        try
        {
            await serviceLayer.LogoutAsync(session.Cookies, cancellationToken);
        }
        catch (SessionExpiredException)
        {
            // Already gone on the Service Layer side, which is what logout wanted.
        }
    }

    /// <summary>
    /// Returns the live session for a token. The timeout mirrors the Service Layer's:
    /// every valid request restarts it (guide p.16-17).
    /// </summary>
    public UserSession RequireSession(string? token)
    {
        var session = token is null ? null : sessions.Find(token);
        if (session is null)
        {
            throw new SessionExpiredException();
        }

        var now = clock.GetUtcNow();
        if (session.IsExpiredAt(now))
        {
            sessions.Remove(session.Token);
            throw new SessionExpiredException();
        }

        var refreshed = session with { LastAccess = now };
        sessions.Save(refreshed);
        return refreshed;
    }

    // 256 random bits: a token that cannot be guessed, unlike a counter or a timestamp.
    private static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
}
