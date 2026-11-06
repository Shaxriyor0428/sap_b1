using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using MockSl.Domain.Errors;

namespace MockSl.Api.Http;

/// <summary>Reads request bodies and writes responses the way the Service Layer does.</summary>
public static class SlJson
{
    public const string ContentType = "application/json;charset=utf-8";

    public const string ApiRoot = "/b1s/v2";

    // Keeps apostrophes and non-Latin letters readable in responses instead of \uXXXX.
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // The guide's own samples contain trailing commas (p.24-25).
    private static readonly JsonDocumentOptions ReadOptions = new()
    {
        AllowTrailingCommas = true,
    };

    public static async Task<JsonDocument> ReadBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await JsonDocument.ParseAsync(request.Body, ReadOptions, cancellationToken);
        }
        catch (JsonException)
        {
            throw SapErrors.InvalidRequestBody();
        }
    }

    public static string Serialize(JsonNode node) => node.ToJsonString(WriteOptions);

    public static IResult Result(JsonNode body, int statusCode = StatusCodes.Status200OK) =>
        Results.Text(Serialize(body), ContentType, statusCode: statusCode);

    public static string RootUrl(HttpRequest request) => $"{request.Scheme}://{request.Host}{ApiRoot}";
}
