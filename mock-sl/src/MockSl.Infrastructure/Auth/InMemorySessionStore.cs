using System.Collections.Concurrent;
using MockSl.Application.Auth;

namespace MockSl.Infrastructure.Auth;

/// <summary>Sessions live in memory, so restarting the mock logs everyone out.</summary>
public sealed class InMemorySessionStore : ISessionStore
{
    private readonly ConcurrentDictionary<string, Session> _sessions = new();

    public void Save(Session session) => _sessions[session.Id] = session;

    public Session? Find(string sessionId) => _sessions.GetValueOrDefault(sessionId);

    public void Remove(string sessionId) => _sessions.TryRemove(sessionId, out _);

    public void RemoveIdleSince(DateTimeOffset cutoff)
    {
        foreach (var session in _sessions.Values.Where(session => session.LastAccess < cutoff))
        {
            _sessions.TryRemove(session.Id, out _);
        }
    }
}
