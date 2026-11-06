using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using MockSl.Api.Http;
using MockSl.Application.Auth;
using MockSl.Application.Entities;
using MockSl.Application.OData;
using MockSl.Application.SqlQueries;
using MockSl.Domain.Errors;
using MockSl.Domain.SqlQueries;

namespace MockSl.Api.Endpoints;

/// <summary>What the generic resource route cannot do for SQLQueries: permissions and the List function.</summary>
public static class SqlQueryEndpoints
{
    private const string ListAction = "List";
    private const string SkipOption = "$skip";

    public static void EnsurePermitted(HttpContext http, AuthService auth, ResourcePath path)
    {
        var session = auth.RequireSession(http.Request.Cookies[SessionFilter.SessionCookie]);
        var isPlainRead = http.Request.Method == HttpMethods.Get && path.Action is null;

        SqlQueryPolicy.EnsurePermitted(session.IsSuperUser, isPlainRead);
    }

    public static bool IsList(ResourcePath path) => path.Action == ListAction;

    /// <summary>GET or POST SQLQueries('code')/List (guide p.143, p.152).</summary>
    public static async Task<IResult> ListAsync(
        IReadOnlyList<FieldValue>? key,
        HttpContext http,
        SqlQueryRunner runner,
        CancellationToken cancellationToken)
    {
        var request = http.Request;

        if (key is null || (request.Method != HttpMethods.Get && request.Method != HttpMethods.Post))
        {
            throw SapErrors.ActionNotSupported();
        }

        var parameters = request.Method == HttpMethods.Post
            ? ParamListParser.Parse(await ReadParamListAsync(request, cancellationToken))
            : ParamListParser.FromQuery(ReadQueryParameters(request));

        var preferredPageSize = PreferHeader.ReadMaxPageSize(request.Headers["Prefer"]);
        var plan = new PagePlan(null, ReadSkip(request), preferredPageSize ?? PagePlan.DefaultPageSize);

        var result = await runner.ListAsync(key, parameters, plan, cancellationToken);

        if (preferredPageSize is { } pageSize)
        {
            http.Response.Headers["Preference-Applied"] = $"odata.maxpagesize={pageSize}";
        }

        var body = new JsonObject
        {
            ["@odata.context"] = $"{SlJson.RootUrl(request)}/$metadata#SAPB1.SQLQueryResult",
            ["SqlText"] = result.SqlText,
            ["value"] = result.Value,
        };

        if (result.NextLink is { } nextLink)
        {
            body["@odata.nextLink"] = nextLink;
        }

        return SlJson.Result(body);
    }

    // The body is optional: a query without parameters is run with an empty POST.
    private static async Task<string?> ReadParamListAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(request.Body);
        var text = await reader.ReadToEndAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(text);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw SapErrors.InvalidRequestBody();
            }

            if (!document.RootElement.TryGetProperty("ParamList", out var paramList) || paramList.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            return paramList.ValueKind == JsonValueKind.String ? paramList.GetString() : throw SapErrors.SqlParameterError();
        }
        catch (JsonException)
        {
            throw SapErrors.InvalidRequestBody();
        }
    }

    // A dollar sign marks an OData option. Everything else in the query string is a parameter of the SQL.
    private static IEnumerable<KeyValuePair<string, string>> ReadQueryParameters(HttpRequest request) =>
        request.Query
            .Where(option => !option.Key.StartsWith('$'))
            .Select(option => KeyValuePair.Create(option.Key, option.Value.ToString()));

    private static int ReadSkip(HttpRequest request)
    {
        foreach (var option in request.Query.Where(option => option.Key.StartsWith('$')))
        {
            if (option.Key != SkipOption)
            {
                throw SapErrors.UnsupportedQueryOption(option.Key);
            }
        }

        var skip = request.Query[SkipOption].ToString();

        if (skip.Length == 0)
        {
            return 0;
        }

        return int.TryParse(skip, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw SapErrors.InvalidQueryOption(SkipOption, skip);
    }
}
