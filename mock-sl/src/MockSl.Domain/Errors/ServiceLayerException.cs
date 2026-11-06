namespace MockSl.Domain.Errors;

/// <summary>
/// An error the client must see in the SAP error format.
/// Any other exception is a bug in the mock and becomes a 500.
/// </summary>
public sealed class ServiceLayerException(ServiceLayerErrorKind kind, string code, string message)
    : Exception(message)
{
    public ServiceLayerErrorKind Kind { get; } = kind;

    public string Code { get; } = code;
}
