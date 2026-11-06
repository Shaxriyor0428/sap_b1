using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Sap.Api.Http;
using Sap.Application.Auth;
using Sap.Application.Errors;
using Sap.Application.ServiceLayer;
using Sap.Infrastructure.Auth;

namespace Sap.UnitTests.Http;

public class ApiErrorMiddlewareTests
{
    private readonly InMemorySessionStore _sessions = new();

    private async Task<(int Status, string Code, string Message)> RunAsync(Exception thrown, HttpContext? context = null)
    {
        context ??= new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var middleware = new ApiErrorMiddleware(_ => throw thrown, NullLogger<ApiErrorMiddleware>.Instance);
        await middleware.InvokeAsync(context, _sessions);

        context.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(context.Response.Body);

        return (
            context.Response.StatusCode,
            body.RootElement.GetProperty("code").GetString()!,
            body.RootElement.GetProperty("message").GetString()!);
    }

    [Fact]
    public async Task Service_layer_error_keeps_its_status_code_and_text()
    {
        var result = await RunAsync(new ServiceLayerErrorException(400, "-10", "Already assigned"));

        Assert.Equal((400, "-10", "Already assigned"), result);
    }

    [Fact]
    public async Task Expired_session_asks_the_user_to_log_in_again()
    {
        var result = await RunAsync(new SessionExpiredException());

        Assert.Equal(401, result.Status);
        Assert.Equal("SESSION_EXPIRED", result.Code);
    }

    [Fact]
    public async Task Expired_session_also_removes_our_token()
    {
        var session = new UserSession("token", "manager", new SlCookies("s", null), TimeSpan.FromMinutes(30), DateTimeOffset.UnixEpoch);
        _sessions.Save(session);

        var context = new DefaultHttpContext();
        context.SetUserSession(session);

        await RunAsync(new SessionExpiredException(), context);

        Assert.Null(_sessions.Find("token"));
    }

    [Theory]
    [MemberData(nameof(ApplicationErrors))]
    public async Task Application_errors_map_to_their_status(Exception thrown, int expectedStatus, string expectedCode)
    {
        var result = await RunAsync(thrown);

        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal(expectedCode, result.Code);
    }

    public static TheoryData<Exception, int, string> ApplicationErrors() => new()
    {
        { new CustomerNotFoundException("S1"), 404, "CUSTOMER_NOT_FOUND" },
        { new InvalidRequestException("bad"), 400, "INVALID_REQUEST" },
        { new ServiceLayerUnavailableException(new HttpRequestException()), 502, "SERVICE_LAYER_UNAVAILABLE" },
    };

    [Fact]
    public async Task Unexpected_error_hides_its_details_from_the_client()
    {
        var result = await RunAsync(new InvalidOperationException("Host=secret-server;Password=hunter2"));

        Assert.Equal(500, result.Status);
        Assert.Equal("INTERNAL_ERROR", result.Code);
        Assert.DoesNotContain("hunter2", result.Message);
    }

    [Fact]
    public async Task Outage_hides_the_internal_address_from_the_client()
    {
        var outage = new ServiceLayerUnavailableException(new HttpRequestException("No route to 10.0.0.5:50000"));

        var result = await RunAsync(outage);

        Assert.DoesNotContain("10.0.0.5", result.Message);
    }
}
