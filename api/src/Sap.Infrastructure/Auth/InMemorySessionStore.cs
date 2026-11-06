using System.Collections.Concurrent;
using Sap.Application.Auth;

namespace Sap.Infrastructure.Auth;

/// <summary>Sessions live in memory, so restarting the backend means logging in again.</summary>
public sealed class InMemorySessionStore : ISessionStore
{
    private readonly ConcurrentDictionary<string, UserSession> _sessions = new();

    public void Save(UserSession session) => _sessions[session.Token] = session;

    public UserSession? Find(string token) => _sessions.GetValueOrDefault(token);

    public void Remove(string token) => _sessions.TryRemove(token, out _);

    public void RemoveExpired(DateTimeOffset now)
    {
        foreach (var session in _sessions.Values.Where(session => session.IsExpiredAt(now)))
        {
            _sessions.TryRemove(session.Token, out _);
        }
    }
}
