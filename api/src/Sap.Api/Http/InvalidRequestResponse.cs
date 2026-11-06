using Microsoft.AspNetCore.Mvc;

namespace Sap.Api.Http;

/// <summary>
/// ASP.NET rejects unreadable requests before a controller runs, for example broken JSON or page=abc.
/// This gives those rejections the same ApiError shape as every other error.
/// </summary>
public static class InvalidRequestResponse
{
    public static IActionResult Create(ActionContext context)
    {
        var message = context.ModelState.Values
            .SelectMany(entry => entry.Errors)
            .Select(error => error.ErrorMessage)
            .FirstOrDefault(text => !string.IsNullOrEmpty(text));

        return new BadRequestObjectResult(new ApiError("INVALID_REQUEST", message ?? "The request is not valid."));
    }
}
