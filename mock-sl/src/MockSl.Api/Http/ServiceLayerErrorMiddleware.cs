using System.Text.Json.Nodes;
using MockSl.Domain.Errors;

namespace MockSl.Api.Http;

/// <summary>
/// Turns exceptions into the SAP error format, using the v2 shape from guide p.162:
/// {"error":{"code":"-2039","message":"..."}}
/// </summary>
public sealed class ServiceLayerErrorMiddleware(
    RequestDelegate next,
    ILogger<ServiceLayerErrorMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ServiceLayerException exception)
        {
            await WriteErrorAsync(context, ToStatusCode(exception.Kind), exception.Code, exception.Message);
        }
        catch (Exception exception) when (!context.RequestAborted.IsCancellationRequested)
        {
            logger.LogError(exception, "Unhandled error for {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, "-1", "Internal server error");
        }
    }

    private static int ToStatusCode(ServiceLayerErrorKind kind) => kind switch
    {
        ServiceLayerErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
        ServiceLayerErrorKind.NotFound => StatusCodes.Status404NotFound,
        _ => StatusCodes.Status400BadRequest,
    };

    private static async Task WriteErrorAsync(HttpContext context, int statusCode, string code, string message)
    {
        var body = new JsonObject
        {
            ["error"] = new JsonObject
            {
                ["code"] = code,
                ["message"] = message,
            },
        };

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = SlJson.ContentType;
        await context.Response.WriteAsync(SlJson.Serialize(body));
    }
}
