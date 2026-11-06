namespace MockSl.Application.Auth;

public sealed record Session(string Id, string UserCode, bool IsSuperUser, DateTimeOffset LastAccess);

public sealed record UserAccount(string UserCode, bool IsSuperUser);

public sealed record AuthOptions(string CompanyDb, int SessionTimeoutMinutes);

/// <summary>Port to the OUSR table, implemented in Infrastructure.</summary>
public interface IUserStore
{
    /// <summary>Returns the user if the credentials match and the account is not locked.</summary>
    Task<UserAccount?> FindByCredentialsAsync(string userName, string password, CancellationToken cancellationToken);
}

/// <summary>Port to wherever sessions are kept, implemented in Infrastructure.</summary>
public interface ISessionStore
{
    void Save(Session session);

    Session? Find(string sessionId);

    void Remove(string sessionId);

    void RemoveIdleSince(DateTimeOffset cutoff);
}
