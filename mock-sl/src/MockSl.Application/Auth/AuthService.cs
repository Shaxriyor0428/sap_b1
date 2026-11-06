using MockSl.Domain.Errors;

namespace MockSl.Application.Auth;

/// <summary>
/// Login, logout and session checks. The timeout is sliding: every valid request
/// restarts it, so a session ends only after that many idle minutes (guide p.16-17).
/// </summary>
public sealed class AuthService(
    IUserStore users,
    ISessionStore sessions,
    AuthOptions options,
    TimeProvider clock)
{
    private TimeSpan Timeout => TimeSpan.FromMinutes(options.SessionTimeoutMinutes);

    public int SessionTimeoutMinutes => options.SessionTimeoutMinutes;

    public async Task<Session> LoginAsync(
        string? companyDb,
        string? userName,
        string? password,
        CancellationToken cancellationToken)
    {
        if (companyDb != options.CompanyDb || string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password))
        {
            throw SapErrors.LoginFailed();
        }

        var user = await users.FindByCredentialsAsync(userName, password, cancellationToken)
            ?? throw SapErrors.LoginFailed();

        var now = clock.GetUtcNow();
        sessions.RemoveIdleSince(now - Timeout);

        var session = new Session(Guid.NewGuid().ToString(), user.UserCode, user.IsSuperUser, now);
        sessions.Save(session);
        return session;
    }

    public void Logout(string sessionId) => sessions.Remove(sessionId);

    /// <summary>Returns the live session or throws the 401 error from guide p.17.</summary>
    public Session RequireSession(string? sessionId)
    {
        var session = sessionId is null ? null : sessions.Find(sessionId);
        if (session is null)
        {
            throw SapErrors.InvalidSession();
        }

        var now = clock.GetUtcNow();
        if (now - session.LastAccess > Timeout)
        {
            sessions.Remove(session.Id);
            throw SapErrors.InvalidSession();
        }

        var refreshed = session with { LastAccess = now };
        sessions.Save(refreshed);
        return refreshed;
    }
}
