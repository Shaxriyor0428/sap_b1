namespace Sap.Api.Http;

/// <summary>The one error shape of this API, whether the error came from the Service Layer or from here.</summary>
public sealed record ApiError(string Code, string Message);
