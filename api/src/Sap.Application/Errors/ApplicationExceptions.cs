namespace Sap.Application.Errors;

/// <summary>The Service Layer answered with an error. Its status, code and text go to the client unchanged.</summary>
public sealed class ServiceLayerErrorException(int statusCode, string code, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public string Code { get; } = code;
}

/// <summary>The Service Layer could not be reached or did not answer in time.</summary>
public sealed class ServiceLayerUnavailableException(Exception innerException)
    : Exception("The Service Layer is not available.", innerException);

/// <summary>The token is missing or unknown, or the Service Layer no longer accepts the session behind it.</summary>
public sealed class SessionExpiredException()
    : Exception("The session is missing or has expired. Log in again.");

/// <summary>The request itself is wrong, so there is no reason to call the Service Layer.</summary>
public sealed class InvalidRequestException(string message) : Exception(message);

/// <summary>The business partner exists but is not a customer, so this API must not touch it.</summary>
public sealed class CustomerNotFoundException(string code) : Exception($"Customer '{code}' was not found.");
