using System.Net;
using System.Text.Json;
using Sap.Application.Errors;
using Sap.Application.ServiceLayer;
using Sap.Infrastructure.ServiceLayer;

namespace Sap.UnitTests.ServiceLayer;

public class ServiceLayerClientTests
{
    private static readonly SlCookies Cookies = new("sl-session", ".node1");

    private readonly StubHandler _handler = new();

    private ServiceLayerClient NewClient(string baseUrl = "https://sap.example:50000/b1s/v2/")
    {
        var options = ServiceLayerOptions.Create(baseUrl, "TESTDB", skipTlsVerify: false);
        return new ServiceLayerClient(new HttpClient(_handler) { BaseAddress = options.BaseAddress }, options);
    }

    private static BusinessPartnerQuery ListQuery() => new("CardType eq 'cCustomer'", "CardCode", 21, 40);

    [Fact]
    public async Task Login_posts_the_company_from_settings_and_the_given_credentials()
    {
        _handler.Respond(HttpStatusCode.OK, """{"SessionId":"abc","SessionTimeout":30}""");

        await NewClient().LoginAsync("manager", "secret", default);

        Assert.Equal(HttpMethod.Post, _handler.Request!.Method);
        Assert.Equal("https://sap.example:50000/b1s/v2/Login", _handler.Request.RequestUri!.AbsoluteUri);
        Assert.Equal("""{"CompanyDB":"TESTDB","UserName":"manager","Password":"secret"}""", _handler.Body);
    }

    [Fact]
    public async Task Login_reads_the_session_and_the_route_cookie()
    {
        _handler.Respond(
            HttpStatusCode.OK,
            """{"SessionId":"abc","Version":"1000110","SessionTimeout":45}""",
            setCookies: ["B1SESSION=abc;HttpOnly;", "ROUTEID=.node7; path=/b1s"]);

        var login = await NewClient().LoginAsync("manager", "secret", default);

        Assert.Equal(new SlCookies("abc", ".node7"), login.Cookies);
        Assert.Equal(45, login.SessionTimeoutMinutes);
    }

    [Fact]
    public async Task Login_works_without_a_route_cookie_because_it_is_optional()
    {
        _handler.Respond(HttpStatusCode.OK, """{"SessionId":"abc","SessionTimeout":30}""");

        var login = await NewClient().LoginAsync("manager", "secret", default);

        Assert.Null(login.Cookies.RouteId);
    }

    [Fact]
    public async Task Failed_login_reports_the_service_layer_message_not_an_expired_session()
    {
        _handler.Respond(HttpStatusCode.Unauthorized, """{"error":{"code":"-1","message":"Login failed"}}""");

        var error = await Assert.ThrowsAsync<ServiceLayerErrorException>(() =>
            NewClient().LoginAsync("manager", "wrong", default));

        Assert.Equal(401, error.StatusCode);
        Assert.Equal("Login failed", error.Message);
    }

    [Fact]
    public async Task Base_url_works_without_the_final_slash()
    {
        _handler.Respond(HttpStatusCode.OK, """{"SessionId":"abc","SessionTimeout":30}""");

        await NewClient("https://sap.example:50000/b1s/v2").LoginAsync("manager", "secret", default);

        Assert.Equal("https://sap.example:50000/b1s/v2/Login", _handler.Request!.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task List_builds_the_odata_query_and_keeps_the_dollar_sign_literal()
    {
        _handler.Respond(HttpStatusCode.OK, """{"value":[]}""");

        await NewClient().ListBusinessPartnersAsync(Cookies, ListQuery(), default);

        var query = _handler.Request!.RequestUri!.Query;
        Assert.StartsWith("?$filter=", query);
        Assert.Equal(
            "?$filter=CardType eq 'cCustomer'&$orderby=CardCode&$top=21&$skip=40",
            Uri.UnescapeDataString(query));
    }

    [Fact]
    public async Task List_escapes_characters_that_would_change_the_query()
    {
        _handler.Respond(HttpStatusCode.OK, """{"value":[]}""");
        var query = new BusinessPartnerQuery("contains(Phone1,'+998&$top=1')", "CardCode", 21, 0);

        await NewClient().ListBusinessPartnersAsync(Cookies, query, default);

        var sent = _handler.Request!.RequestUri!.Query;
        Assert.DoesNotContain("+998", sent);
        Assert.DoesNotContain("&$top=1", sent);
        Assert.Contains("%2B998", sent);
    }

    [Fact]
    public async Task List_asks_for_a_page_as_large_as_top()
    {
        _handler.Respond(HttpStatusCode.OK, """{"value":[]}""");

        await NewClient().ListBusinessPartnersAsync(Cookies, ListQuery(), default);

        Assert.Equal("odata.maxpagesize=21", Assert.Single(_handler.Request!.Headers.GetValues("Prefer")));
    }

    [Fact]
    public async Task List_reads_the_fields_it_knows_and_ignores_the_hundreds_of_others()
    {
        _handler.Respond(HttpStatusCode.OK, """
            {
              "@odata.context": "x",
              "value": [
                {
                  "@odata.etag": "W/\"1\"",
                  "CardCode": "C1", "CardName": "Ali", "CardType": "cCustomer", "Phone1": "90",
                  "Cellular": null, "EmailAddress": "a@b.uz", "Address": "A", "City": "T",
                  "CurrentAccountBalance": 1500.5, "GroupCode": 100, "ContactEmployees": [], "U_Region": "Toshkent"
                }
              ],
              "@odata.nextLink": "BusinessPartners?$skip=20"
            }
            """);

        var partner = Assert.Single(await NewClient().ListBusinessPartnersAsync(Cookies, ListQuery(), default));

        Assert.Equal(new BusinessPartner("C1", "Ali", "cCustomer", "90", null, "a@b.uz", "A", "T", 1500.5m, "Toshkent"), partner);
    }

    [Fact]
    public async Task Every_request_carries_both_session_cookies()
    {
        _handler.Respond(HttpStatusCode.OK, """{"value":[]}""");

        await NewClient().ListBusinessPartnersAsync(Cookies, ListQuery(), default);

        Assert.Equal(
            "B1SESSION=sl-session; ROUTEID=.node1",
            Assert.Single(_handler.Request!.Headers.GetValues("Cookie")));
    }

    [Fact]
    public async Task Cookie_header_leaves_out_a_missing_route_id()
    {
        _handler.Respond(HttpStatusCode.OK, """{"value":[]}""");

        await NewClient().ListBusinessPartnersAsync(new SlCookies("sl-session", null), ListQuery(), default);

        Assert.Equal("B1SESSION=sl-session", Assert.Single(_handler.Request!.Headers.GetValues("Cookie")));
    }

    [Fact]
    public async Task Two_users_on_one_client_keep_their_own_sessions()
    {
        var client = NewClient();

        _handler.Respond(HttpStatusCode.OK, """{"value":[]}""");
        await client.ListBusinessPartnersAsync(new SlCookies("user-a", null), ListQuery(), default);
        var first = Assert.Single(_handler.Request!.Headers.GetValues("Cookie"));

        _handler.Respond(HttpStatusCode.OK, """{"value":[]}""");
        await client.ListBusinessPartnersAsync(new SlCookies("user-b", null), ListQuery(), default);
        var second = Assert.Single(_handler.Request!.Headers.GetValues("Cookie"));

        Assert.Equal("B1SESSION=user-a", first);
        Assert.Equal("B1SESSION=user-b", second);
    }

    [Fact]
    public async Task Create_sends_service_layer_names_without_nulls_and_with_a_content_length()
    {
        _handler.Respond(HttpStatusCode.Created, """{"CardCode":"C1","CardName":"Ali","CardType":"cCustomer"}""");
        var partner = new BusinessPartnerWrite { CardCode = "C1", CardName = "Ali", CardType = "cCustomer" };

        var created = await NewClient().CreateBusinessPartnerAsync(Cookies, partner, default);

        Assert.Equal(HttpMethod.Post, _handler.Request!.Method);
        Assert.Equal("""{"CardCode":"C1","CardName":"Ali","CardType":"cCustomer"}""", _handler.Body);
        Assert.NotNull(_handler.ContentLength);
        Assert.Equal("C1", created.CardCode);
    }

    [Fact]
    public async Task Update_patches_only_the_given_fields()
    {
        _handler.Respond(HttpStatusCode.NoContent, string.Empty);

        await NewClient().UpdateBusinessPartnerAsync(Cookies, "C1", new BusinessPartnerWrite { City = "Samarqand" }, default);

        Assert.Equal(HttpMethod.Patch, _handler.Request!.Method);
        Assert.EndsWith("/BusinessPartners('C1')", _handler.Request.RequestUri!.AbsoluteUri);
        Assert.Equal("""{"City":"Samarqand"}""", _handler.Body);
    }

    [Fact]
    public async Task Delete_targets_the_given_key()
    {
        _handler.Respond(HttpStatusCode.NoContent, string.Empty);

        await NewClient().DeleteBusinessPartnerAsync(Cookies, "C1", default);

        Assert.Equal(HttpMethod.Delete, _handler.Request!.Method);
        Assert.EndsWith("/BusinessPartners('C1')", _handler.Request.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task Card_type_is_read_with_a_narrow_select()
    {
        _handler.Respond(HttpStatusCode.OK, """{"CardCode":"S1","CardType":"cSupplier"}""");

        var cardType = await NewClient().GetCardTypeAsync(Cookies, "S1", default);

        Assert.Equal("cSupplier", cardType);
        Assert.EndsWith("/BusinessPartners('S1')?$select=CardCode,CardType", _handler.Request!.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task Apostrophe_in_a_key_is_doubled()
    {
        _handler.Respond(HttpStatusCode.NoContent, string.Empty);

        await NewClient().DeleteBusinessPartnerAsync(Cookies, "O'Brien", default);

        Assert.Equal("BusinessPartners('O''Brien')", Uri.UnescapeDataString(_handler.Request!.RequestUri!.Segments[^1]));
    }

    [Theory]
    [InlineData("a/../../Logout")]
    [InlineData("a?x=1")]
    [InlineData("a#b")]
    public async Task A_key_cannot_change_which_url_is_called(string code)
    {
        _handler.Respond(HttpStatusCode.NoContent, string.Empty);

        await NewClient().DeleteBusinessPartnerAsync(Cookies, code, default);

        var uri = _handler.Request!.RequestUri!;
        Assert.StartsWith("/b1s/v2/BusinessPartners(", uri.AbsolutePath);
        Assert.Empty(uri.Query);
        Assert.Empty(uri.Fragment);
    }

    [Fact]
    public async Task A_401_on_a_session_request_means_the_session_expired()
    {
        _handler.Respond(HttpStatusCode.Unauthorized, """{"error":{"code":"301","message":"Invalid session"}}""");

        await Assert.ThrowsAsync<SessionExpiredException>(() =>
            NewClient().ListBusinessPartnersAsync(Cookies, ListQuery(), default));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, """{"error":{"code":"-10","message":"Already exists"}}""", 400, "-10", "Already exists")]
    [InlineData(HttpStatusCode.NotFound, """{"error":{"code":-2028,"message":{"lang":"en-us","value":"No match"}}}""", 404, "-2028", "No match")]
    public async Task Service_layer_errors_keep_their_status_code_and_text(
        HttpStatusCode status,
        string body,
        int expectedStatus,
        string expectedCode,
        string expectedMessage)
    {
        _handler.Respond(status, body);

        var error = await Assert.ThrowsAsync<ServiceLayerErrorException>(() =>
            NewClient().DeleteBusinessPartnerAsync(Cookies, "C1", default));

        Assert.Equal(expectedStatus, error.StatusCode);
        Assert.Equal(expectedCode, error.Code);
        Assert.Equal(expectedMessage, error.Message);
    }

    [Fact]
    public async Task Connection_failure_is_reported_as_unavailable()
    {
        _handler.Fail(new HttpRequestException("Connection refused"));

        await Assert.ThrowsAsync<ServiceLayerUnavailableException>(() =>
            NewClient().LoginAsync("manager", "secret", default));
    }

    [Fact]
    public async Task Timeout_is_reported_as_unavailable()
    {
        _handler.Fail(new TaskCanceledException());

        await Assert.ThrowsAsync<ServiceLayerUnavailableException>(() =>
            NewClient().ListBusinessPartnersAsync(Cookies, ListQuery(), default));
    }

    [Fact]
    public async Task Cancellation_by_the_caller_is_not_reported_as_an_outage()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        _handler.Fail(new TaskCanceledException());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            NewClient().ListBusinessPartnersAsync(Cookies, ListQuery(), cancellation.Token));
    }

    [Fact]
    public void Cookies_are_never_stored_in_the_shared_handler()
    {
        using var handler = ServiceLayerHttpHandler.Create(skipTlsVerify: false);

        Assert.False(handler.UseCookies);
    }

    [Fact]
    public void The_response_model_matches_the_service_layer_names()
    {
        var partner = JsonSerializer.Deserialize<BusinessPartner>("""{"CardCode":"C1","EmailAddress":"a@b.uz"}""");

        Assert.Equal("a@b.uz", partner!.EmailAddress);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private HttpResponseMessage? _response;
        private Exception? _failure;

        public HttpRequestMessage? Request { get; private set; }

        public string? Body { get; private set; }

        public long? ContentLength { get; private set; }

        public void Respond(HttpStatusCode status, string body, string[]? setCookies = null)
        {
            _failure = null;
            _response = new HttpResponseMessage(status)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            };

            foreach (var cookie in setCookies ?? [])
            {
                _response.Headers.Add("Set-Cookie", cookie);
            }
        }

        public void Fail(Exception failure) => _failure = failure;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;

            if (request.Content is not null)
            {
                Body = await request.Content.ReadAsStringAsync(cancellationToken);
                ContentLength = request.Content.Headers.ContentLength;
            }

            if (_failure is not null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw _failure;
            }

            return _response!;
        }
    }
}
