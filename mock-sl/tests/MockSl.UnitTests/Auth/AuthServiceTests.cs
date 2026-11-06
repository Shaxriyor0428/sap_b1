using MockSl.Application.Auth;
using MockSl.Domain.Errors;
using MockSl.Infrastructure.Auth;

namespace MockSl.UnitTests.Auth;

public class AuthServiceTests
{
    private readonly FakeClock _clock = new();
    private readonly AuthService _auth;

    public AuthServiceTests()
    {
        _auth = new AuthService(
            new FakeUserStore(),
            new InMemorySessionStore(),
            new AuthOptions("TEST", SessionTimeoutMinutes: 30),
            _clock);
    }

    private Task<Session> LoginAsync() => _auth.LoginAsync("TEST", "manager", "1234", default);

    [Fact]
    public async Task Valid_credentials_open_a_session()
    {
        var session = await LoginAsync();

        Assert.Equal("manager", session.UserCode);
        Assert.True(session.IsSuperUser);
        Assert.Equal(session.Id, _auth.RequireSession(session.Id).Id);
    }

    [Fact]
    public async Task Every_login_gets_its_own_session()
    {
        Assert.NotEqual((await LoginAsync()).Id, (await LoginAsync()).Id);
    }

    [Theory]
    [InlineData("OTHER", "manager", "1234")]
    [InlineData("TEST", "manager", "wrong")]
    [InlineData("TEST", "nobody", "1234")]
    [InlineData("TEST", null, "1234")]
    [InlineData("TEST", "manager", null)]
    [InlineData(null, "manager", "1234")]
    public async Task Invalid_login_is_rejected(string? companyDb, string? userName, string? password)
    {
        var error = await Assert.ThrowsAsync<ServiceLayerException>(() =>
            _auth.LoginAsync(companyDb, userName, password, default));

        Assert.Equal(ServiceLayerErrorKind.Unauthorized, error.Kind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-session")]
    public void Unknown_session_returns_the_error_from_the_guide(string? sessionId)
    {
        var error = Assert.Throws<ServiceLayerException>(() => _auth.RequireSession(sessionId));

        Assert.Equal("301", error.Code);
        Assert.Equal("Invalid session or session already timeout.", error.Message);
    }

    [Fact]
    public async Task Session_expires_after_the_idle_timeout()
    {
        var session = await LoginAsync();

        _clock.Advance(TimeSpan.FromMinutes(31));

        Assert.Throws<ServiceLayerException>(() => _auth.RequireSession(session.Id));
    }

    [Fact]
    public async Task Activity_restarts_the_timeout()
    {
        var session = await LoginAsync();

        _clock.Advance(TimeSpan.FromMinutes(20));
        _auth.RequireSession(session.Id);
        _clock.Advance(TimeSpan.FromMinutes(20));

        Assert.Equal(session.Id, _auth.RequireSession(session.Id).Id);
    }

    [Fact]
    public async Task Logout_ends_the_session()
    {
        var session = await LoginAsync();

        _auth.Logout(session.Id);

        Assert.Throws<ServiceLayerException>(() => _auth.RequireSession(session.Id));
    }

    private sealed class FakeClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    private sealed class FakeUserStore : IUserStore
    {
        public Task<UserAccount?> FindByCredentialsAsync(
            string userName,
            string password,
            CancellationToken cancellationToken) =>
            Task.FromResult(userName == "manager" && password == "1234"
                ? new UserAccount("manager", IsSuperUser: true)
                : null);
    }
}
