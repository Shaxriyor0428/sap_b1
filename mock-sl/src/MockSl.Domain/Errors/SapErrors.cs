using static MockSl.Domain.Errors.ServiceLayerErrorKind;

namespace MockSl.Domain.Errors;

/// <summary>
/// Every error the mock can return, in one place.
/// Each entry states where its code and text come from, see docs/sl-contract.md.
/// "Mock choice" means no public source documents the exact text, so a real SAP
/// check may report a difference. Fixing it only touches this file.
/// </summary>
public static class SapErrors
{
    /// <summary>Guide p.17.</summary>
    public static ServiceLayerException InvalidSession() =>
        new(Unauthorized, "301", "Invalid session or session already timeout.");

    /// <summary>Mock choice: the guide does not document a failed login.</summary>
    public static ServiceLayerException LoginFailed() =>
        new(Unauthorized, "-1", "Login failed");

    /// <summary>Code and status from guide p.80. The text is a mock choice.</summary>
    public static ServiceLayerException EntityNotFound() =>
        new(NotFound, "-2028", "No matching records found (ODBC -2028)");

    /// <summary>Guide p.28.</summary>
    public static ServiceLayerException ActionNotSupported() =>
        new(BadRequest, "-5006", "The requested action is not supported for this object.");

    /// <summary>Code from guide p.25, text supplied by the entity definition.</summary>
    public static ServiceLayerException AlreadyExists(string message) =>
        new(BadRequest, "-10", message);

    /// <summary>Code -10 is the business validation code (guide p.25). The text is a mock choice.</summary>
    public static ServiceLayerException RequiredProperty(string typeName, string property) =>
        new(BadRequest, "-10", $"Property '{property}' of '{typeName}' is required");

    // The guide uses -1000 for malformed requests (p.47, p.77).
    // The texts of the -1000 errors below are mock choices.

    public static ServiceLayerException InvalidProperty(string typeName, string property) =>
        new(BadRequest, "-1000", $"Property '{property}' of '{typeName}' is invalid");

    public static ServiceLayerException ValueTooLong(string typeName, string property) =>
        new(BadRequest, "-1000", $"Value too long in property '{property}' of '{typeName}'");

    public static ServiceLayerException InvalidValue(string property, string value) =>
        new(BadRequest, "-1000", $"Invalid value '{value}' for property '{property}'");

    public static ServiceLayerException InvalidFilter(string reason) =>
        new(BadRequest, "-1000", $"Invalid $filter: {reason}");

    public static ServiceLayerException InvalidQueryOption(string option, string value) =>
        new(BadRequest, "-1000", $"Invalid value '{value}' for query option '{option}'");

    public static ServiceLayerException UnsupportedQueryOption(string option) =>
        new(BadRequest, "-1000", $"Query option '{option}' is not supported");

    public static ServiceLayerException InvalidRequestBody() =>
        new(BadRequest, "-1000", "The request body is not a valid JSON object");

    public static ServiceLayerException UnrecognizedResource() =>
        new(NotFound, "-1000", "Unrecognized resource path.");
}
