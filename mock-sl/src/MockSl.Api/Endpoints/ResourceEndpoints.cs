using System.Text.Json.Nodes;
using MockSl.Api.Http;
using MockSl.Application.Auth;
using MockSl.Application.Entities;
using MockSl.Application.OData;
using MockSl.Application.SqlQueries;
using MockSl.Domain.Errors;
using MockSl.Domain.Metadata;

namespace MockSl.Api.Endpoints;

/// <summary>
/// One route for all objects: /b1s/v2/{anything}.
/// ASP.NET cannot express BusinessPartners('C0001') as a route pattern,
/// so the path is parsed here and the method decides the operation.
/// </summary>
public static class ResourceEndpoints
{
    public static void MapResourceEndpoints(this IEndpointRouteBuilder app)
    {
        app.Map($"{SlJson.ApiRoot}/{{**resource}}", HandleAsync).AddEndpointFilter<SessionFilter>();
    }

    private static async Task<IResult> HandleAsync(
        string? resource,
        HttpContext http,
        EntityHandler handler,
        AuthService auth,
        SqlQueryRunner sqlQueries,
        CancellationToken cancellationToken)
    {
        var path = ResourcePath.Parse(resource ?? string.Empty);
        var definition = EntityCatalog.Find(path.EntitySet) ?? throw SapErrors.UnrecognizedResource();
        var entity = await handler.WithUserFieldsAsync(definition, cancellationToken);

        var key = path.Key is null ? null : EntityKey.Bind(entity, path.Key);

        if (definition == EntityCatalog.SqlQueries)
        {
            SqlQueryEndpoints.EnsurePermitted(http, auth, path);

            if (SqlQueryEndpoints.IsList(path))
            {
                return await SqlQueryEndpoints.ListAsync(key, http, sqlQueries, cancellationToken);
            }
        }

        if (path.Action is { } action)
        {
            return await RunActionAsync(entity, key, action, http, handler, cancellationToken);
        }

        return (http.Request.Method, key) switch
        {
            ("GET", null) => await ListAsync(entity, http, handler, cancellationToken),
            ("GET", _) => await GetAsync(entity, key, http, handler, cancellationToken),
            ("POST", null) => await CreateAsync(entity, http, handler, cancellationToken),
            ("PATCH", not null) => await UpdateAsync(entity, key, http, handler, cancellationToken),
            ("DELETE", not null) => await DeleteAsync(entity, key, handler, cancellationToken),
            _ => throw SapErrors.ActionNotSupported(),
        };
    }

    private static async Task<IResult> ListAsync(
        EntityDefinition entity,
        HttpContext http,
        EntityHandler handler,
        CancellationToken cancellationToken)
    {
        var options = ReadQueryOptions(http.Request);
        var query = ODataQuery.Parse(entity, options);

        var preferredPageSize = PreferHeader.ReadMaxPageSize(http.Request.Headers["Prefer"]);
        var plan = new PagePlan(query.Top, query.Skip, preferredPageSize ?? PagePlan.DefaultPageSize);

        var page = await handler.ListAsync(entity, query, plan, cancellationToken);

        if (preferredPageSize is { } pageSize)
        {
            http.Response.Headers["Preference-Applied"] = $"odata.maxpagesize={pageSize}";
        }

        var body = new JsonObject
        {
            ["@odata.context"] = $"{SlJson.RootUrl(http.Request)}/$metadata#{entity.SetName}",
            ["value"] = page.Value,
        };

        if (page.Next is { } next)
        {
            body["@odata.nextLink"] = ODataLinks.BuildNextLink(entity.SetName, options, next);
        }

        return SlJson.Result(body);
    }

    private static async Task<IResult> GetAsync(
        EntityDefinition entity,
        IReadOnlyList<FieldValue> key,
        HttpContext http,
        EntityHandler handler,
        CancellationToken cancellationToken)
    {
        var query = ODataQuery.Parse(entity, ReadQueryOptions(http.Request));
        var body = await handler.GetAsync(entity, key, query, cancellationToken);

        body.Insert(0, "@odata.context", $"{SlJson.RootUrl(http.Request)}/$metadata#{entity.SetName}/$entity");
        return SlJson.Result(body);
    }

    private static async Task<IResult> CreateAsync(
        EntityDefinition entity,
        HttpContext http,
        EntityHandler handler,
        CancellationToken cancellationToken)
    {
        using var document = await SlJson.ReadBodyAsync(http.Request, cancellationToken);
        var created = await handler.CreateAsync(entity, document.RootElement, cancellationToken);

        var root = SlJson.RootUrl(http.Request);

        // Uri escapes spaces and non-ASCII letters, which a raw HTTP header could not carry.
        http.Response.Headers.Location = new Uri($"{root}/{entity.SetName}{created.KeyInUrl}").AbsoluteUri;

        created.Value.Insert(0, "@odata.context", $"{root}/$metadata#{entity.SetName}/$entity");
        return SlJson.Result(created.Value, StatusCodes.Status201Created);
    }

    private static async Task<IResult> UpdateAsync(
        EntityDefinition entity,
        IReadOnlyList<FieldValue> key,
        HttpContext http,
        EntityHandler handler,
        CancellationToken cancellationToken)
    {
        using var document = await SlJson.ReadBodyAsync(http.Request, cancellationToken);
        await handler.UpdateAsync(entity, key, document.RootElement, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteAsync(
        EntityDefinition entity,
        IReadOnlyList<FieldValue> key,
        EntityHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.DeleteAsync(entity, key, cancellationToken);
        return Results.NoContent();
    }

    // A bound action needs POST and one object: POST Invoices(1)/Cancel (guide p.29).
    private static async Task<IResult> RunActionAsync(
        EntityDefinition entity,
        IReadOnlyList<FieldValue>? key,
        string action,
        HttpContext http,
        EntityHandler handler,
        CancellationToken cancellationToken)
    {
        if (http.Request.Method != HttpMethods.Post || key is null)
        {
            throw SapErrors.ActionNotSupported();
        }

        await handler.RunActionAsync(entity, key, action, cancellationToken);
        return Results.NoContent();
    }

    private static Dictionary<string, string> ReadQueryOptions(HttpRequest request) =>
        request.Query.ToDictionary(option => option.Key, option => option.Value.ToString());
}
