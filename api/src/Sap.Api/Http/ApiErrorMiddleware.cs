using Sap.Application.Auth;
using Sap.Application.Errors;

namespace Sap.Api.Http;

/// <summary>Turns every exception into one HTTP status and one ApiError body.</summary>
public sealed class ApiErrorMiddleware(RequestDelegate next, ILogger<ApiErrorMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, ISessionStore sessions)
    {
        try
        {
            await next(context);
        }
        catch (SessionExpiredException exception)
        {
            // The Service Layer no longer accepts this session, so our token is useless too.
            if (context.FindUserSession() is { } session)
            {
                sessions.Remove(session.Token);
            }

            await WriteAsync(context, StatusCodes.Status401Unauthorized, "SESSION_EXPIRED", exception.Message);
        }
        catch (ServiceLayerErrorException exception)
        {
            await WriteAsync(context, exception.StatusCode, exception.Code, exception.Message);
        }
        catch (CustomerNotFoundException exception)
        {
            await WriteAsync(context, StatusCodes.Status404NotFound, "CUSTOMER_NOT_FOUND", exception.Message);
        }
        catch (InvalidRequestException exception)
        {
            await WriteAsync(context, StatusCodes.Status400BadRequest, "INVALID_REQUEST", exception.Message);
        }
        catch (ServiceLayerUnavailableException exception)
        {
            logger.LogError(exception, "The Service Layer is not available.");
            await WriteAsync(context, StatusCodes.Status502BadGateway, "SERVICE_LAYER_UNAVAILABLE", exception.Message);
        }
        catch (Exception exception) when (!context.RequestAborted.IsCancellationRequested)
        {
            // The details go to the log only: they may contain internal addresses.
            logger.LogError(exception, "Unhandled error for {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteAsync(context, StatusCodes.Status500InternalServerError, "INTERNAL_ERROR", "An unexpected error occurred.");
        }
    }

    private static Task WriteAsync(HttpContext context, int statusCode, string code, string message)
    {
        context.Response.StatusCode = statusCode;
        return context.Response.WriteAsJsonAsync(new ApiError(code, message));
    }
}
