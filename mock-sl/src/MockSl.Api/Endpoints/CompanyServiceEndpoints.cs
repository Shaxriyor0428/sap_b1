using System.Text.Json;
using System.Text.Json.Nodes;
using MockSl.Api.Http;
using MockSl.Application.Entities;
using MockSl.Application.OData;
using MockSl.Domain.Errors;
using MockSl.Domain.Metadata;

namespace MockSl.Api.Endpoints;

/// <summary>
/// The default accounts (table OACP). The EDMX has no entity set for them, only two global actions,
/// so the routes below are written out by name. The mock only reads them.
/// </summary>
public static class CompanyServiceEndpoints
{
    public static void MapCompanyServiceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet($"{SlJson.ApiRoot}/CompanyService_GetPeriods", GetPeriodsAsync).AddEndpointFilter<SessionFilter>();
        app.MapPost($"{SlJson.ApiRoot}/CompanyService_GetPeriod", GetPeriodAsync).AddEndpointFilter<SessionFilter>();
    }

    private static async Task<IResult> GetPeriodsAsync(EntityHandler handler, CancellationToken cancellationToken)
    {
        var entity = EntityCatalog.PeriodCategories;
        var query = ODataQuery.Parse(entity, new Dictionary<string, string> { ["$select"] = "AbsoluteEntry" });
        var page = await handler.ListAsync(entity, query, new PagePlan(top: null, skip: 0, pageSize: 0), cancellationToken);

        return SlJson.Result(new JsonObject { ["value"] = page.Value });
    }

    private static async Task<IResult> GetPeriodAsync(HttpContext http, EntityHandler handler, CancellationToken cancellationToken)
    {
        using var document = await SlJson.ReadBodyAsync(http.Request, cancellationToken);

        var entity = EntityCatalog.PeriodCategories;
        var key = EntityKey.Of(entity, ReadAbsoluteEntry(document.RootElement));
        var query = ODataQuery.Parse(entity, new Dictionary<string, string>());

        return SlJson.Result(await handler.GetAsync(entity, key, query, cancellationToken));
    }

    private static int ReadAbsoluteEntry(JsonElement body)
    {
        if (body.ValueKind == JsonValueKind.Object
            && body.TryGetProperty("PeriodCategoryParams", out var parameters)
            && parameters.ValueKind == JsonValueKind.Object
            && parameters.TryGetProperty("AbsoluteEntry", out var entry)
            && entry.TryGetInt32(out var absoluteEntry))
        {
            return absoluteEntry;
        }

        throw SapErrors.InvalidRequestBody();
    }
}
