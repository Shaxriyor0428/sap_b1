using Sap.Application.ServiceLayer;

namespace Sap.Application.Auth;

/// <summary>
/// What the backend remembers about a logged-in user. The client only ever sees Token;
/// the Service Layer cookies stay here. The password is never kept.
/// </summary>
public sealed record UserSession(
    string Token,
    string UserName,
    SlCookies Cookies,
    TimeSpan Timeout,
    DateTimeOffset LastAccess)
{
    public bool IsExpiredAt(DateTimeOffset now) => now - LastAccess > Timeout;
}

/// <summary>Port to wherever sessions are kept, implemented in Infrastructure.</summary>
public interface ISessionStore
{
    void Save(UserSession session);

    UserSession? Find(string token);

    void Remove(string token);

    void RemoveExpired(DateTimeOffset now);
}
