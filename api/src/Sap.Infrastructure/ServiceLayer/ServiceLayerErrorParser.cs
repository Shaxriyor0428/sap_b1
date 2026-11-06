using System.Text.Json;
using Sap.Application.Errors;

namespace Sap.Infrastructure.ServiceLayer;

/// <summary>
/// Reads a Service Layer error. Sources show two shapes, so both are accepted, see docs/sl-contract.md:
///   {"error":{"code":"-2039","message":"text"}}                       v2 sample, guide p.162
///   {"error":{"code":-10,"message":{"lang":"en-us","value":"text"}}}  v1 samples, guide p.25
/// A proxy in front of SAP may also answer with something that is not JSON at all.
/// </summary>
public static class ServiceLayerErrorParser
{
    private const string UnknownCode = "UNKNOWN";

    public static ServiceLayerErrorException Parse(int statusCode, string body)
    {
        var fallbackMessage = $"The Service Layer returned HTTP {statusCode}.";

        try
        {
            using var document = JsonDocument.Parse(body);

            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.Object)
            {
                return new ServiceLayerErrorException(
                    statusCode,
                    ReadCode(error) ?? UnknownCode,
                    ReadMessage(error) ?? fallbackMessage);
            }
        }
        catch (JsonException)
        {
            // Not JSON: the fallback below still reports the status.
        }

        return new ServiceLayerErrorException(statusCode, UnknownCode, fallbackMessage);
    }

    private static string? ReadCode(JsonElement error)
    {
        if (!error.TryGetProperty("code", out var code))
        {
            return null;
        }

        return code.ValueKind switch
        {
            JsonValueKind.String => code.GetString(),
            JsonValueKind.Number => code.GetRawText(),
            _ => null,
        };
    }

    private static string? ReadMessage(JsonElement error)
    {
        if (!error.TryGetProperty("message", out var message))
        {
            return null;
        }

        if (message.ValueKind == JsonValueKind.String)
        {
            return message.GetString();
        }

        return message.ValueKind == JsonValueKind.Object
            && message.TryGetProperty("value", out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }
}
