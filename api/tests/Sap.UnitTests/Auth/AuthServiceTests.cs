using System.Text.Json;
using Sap.Application.Auth;
using Sap.Application.Errors;
using Sap.Infrastructure.Auth;
using Sap.UnitTests.Fakes;

namespace Sap.UnitTests.Auth;

public class AuthServiceTests
{
    private readonly FakeClock _clock = new();
    private readonly InMemorySessionStore _sessions = new();

    private AuthService NewService(FakeServiceLayer? serviceLayer = null) =>
        new(serviceLayer ?? new FakeServiceLayer(), _sessions, _clock);

    [Fact]
    public async Task Login_keeps_the_service_layer_cookies_behind_a_token_of_our_own()
    {
        var session = await NewService().LoginAsync("manager", "secret", default);

        Assert.NotEqual("sl-session", session.Token);
        Assert.Equal("sl-session", session.Cookies.B1Session);
        Assert.Equal(".node1", session.Cookies.RouteId);
        Assert.Equal(TimeSpan.FromMinutes(30), session.Timeout);
    }

    [Fact]
    public async Task Tokens_are_long_and_never_repeat()
    {
        var service = NewService();

        var first = await service.LoginAsync("manager", "secret", default);
        var second = await service.LoginAsync("manager", "secret", default);

        Assert.NotEqual(first.Token, second.Token);
        Assert.Equal(64, first.Token.Length);
    }

    [Fact]
    public async Task The_password_is_not_kept_anywhere_in_the_session()
    {
        var session = await NewService().LoginAsync("manager", "very-secret-password", default);

        Assert.DoesNotContain("very-secret-password", JsonSerializer.Serialize(session));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("unknown-token")]
    public void Missing_or_unknown_token_is_rejected(string? token)
    {
        Assert.Throws<SessionExpiredException>(() => NewService().RequireSession(token));
    }

    [Fact]
    public async Task Session_is_found_by_its_token()
    {
        var service = NewService();
        var session = await service.LoginAsync("manager", "secret", default);

        Assert.Equal("manager", service.RequireSession(session.Token).UserName);
    }

    [Fact]
    public async Task Session_expires_after_the_timeout_the_service_layer_reported()
    {
        var service = NewService();
        var session = await service.LoginAsync("manager", "secret", default);

        _clock.Advance(TimeSpan.FromMinutes(31));

        Assert.Throws<SessionExpiredException>(() => service.RequireSession(session.Token));
    }

    [Fact]
    public async Task Activity_restarts_the_timeout()
    {
        var service = NewService();
        var session = await service.LoginAsync("manager", "secret", default);

        _clock.Advance(TimeSpan.FromMinutes(20));
        service.RequireSession(session.Token);
        _clock.Advance(TimeSpan.FromMinutes(20));

        Assert.Equal(session.Token, service.RequireSession(session.Token).Token);
    }

    [Fact]
    public async Task Next_login_clears_sessions_nobody_came_back_for()
    {
        var service = NewService();
        var abandoned = await service.LoginAsync("manager", "secret", default);

        _clock.Advance(TimeSpan.FromMinutes(31));
        await service.LoginAsync("manager", "secret", default);

        Assert.Null(_sessions.Find(abandoned.Token));
    }

    [Fact]
    public async Task Logout_ends_both_our_session_and_the_service_layer_one()
    {
        var serviceLayer = new FakeServiceLayer();
        var service = NewService(serviceLayer);
        var session = await service.LoginAsync("manager", "secret", default);

        await service.LogoutAsync(session, default);

        Assert.True(serviceLayer.LogoutCalled);
        Assert.Throws<SessionExpiredException>(() => service.RequireSession(session.Token));
    }

    [Fact]
    public async Task Logout_succeeds_when_the_service_layer_session_is_already_gone()
    {
        var service = NewService(new FakeServiceLayer { LogoutError = new SessionExpiredException() });
        var session = await service.LoginAsync("manager", "secret", default);

        await service.LogoutAsync(session, default);

        Assert.Null(_sessions.Find(session.Token));
    }

    [Fact]
    public async Task Token_stops_working_even_if_the_service_layer_is_down_during_logout()
    {
        var failure = new ServiceLayerUnavailableException(new HttpRequestException());
        var service = NewService(new FakeServiceLayer { LogoutError = failure });
        var session = await service.LoginAsync("manager", "secret", default);

        await Assert.ThrowsAsync<ServiceLayerUnavailableException>(() => service.LogoutAsync(session, default));

        Assert.Null(_sessions.Find(session.Token));
    }
}
