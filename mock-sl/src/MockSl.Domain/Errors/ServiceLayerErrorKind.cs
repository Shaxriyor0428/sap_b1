namespace MockSl.Domain.Errors;

/// <summary>
/// Category of a Service Layer error. The HTTP layer maps it to a status code,
/// so the domain stays unaware of HTTP.
/// </summary>
public enum ServiceLayerErrorKind
{
    BadRequest,
    Unauthorized,
    NotFound,
}
