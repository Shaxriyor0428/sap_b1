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

    // Business rule errors. Code -10 as above, every text is a mock choice:
    // public sources do not document these messages.

    public static ServiceLayerException ReferenceNotFound(string property, string value) =>
        new(BadRequest, "-10", $"No matching record for '{value}' in property '{property}'");

    public static ServiceLayerException WrongPartnerType(string cardCode) =>
        new(BadRequest, "-10", $"Business partner '{cardCode}' cannot be used for this document type");

    public static ServiceLayerException DocumentWithoutLines() =>
        new(BadRequest, "-10", "The document must contain at least one line");

    public static ServiceLayerException AmountMustBePositive(string property) =>
        new(BadRequest, "-10", $"Property '{property}' must be greater than zero");

    public static ServiceLayerException NotEnoughStock(string itemCode, string warehouse) =>
        new(BadRequest, "-10", $"Quantity falls into negative inventory for item '{itemCode}' in warehouse '{warehouse}'");

    public static ServiceLayerException InstallmentDueDateMissing() =>
        new(BadRequest, "-10", "Every installment must have a DueDate");

    public static ServiceLayerException InstallmentAmountMissing() =>
        new(BadRequest, "-10", "Every installment must have a Total or a Percentage");

    public static ServiceLayerException InstallmentsDoNotMatchTotal() =>
        new(BadRequest, "-10", "The installments must add up to the document total");

    public static ServiceLayerException DocumentAlreadyCancelled() =>
        new(BadRequest, "-10", "The document is already cancelled");

    public static ServiceLayerException DocumentAlreadyClosed() =>
        new(BadRequest, "-10", "The document is already closed");

    public static ServiceLayerException DocumentHasPayments() =>
        new(BadRequest, "-10", "Cancel the payments of this document first");

    public static ServiceLayerException InvoiceOfAnotherPartner(int docEntry) =>
        new(BadRequest, "-10", $"Document {docEntry} belongs to another business partner");

    public static ServiceLayerException InvoiceNotOpen(int docEntry) =>
        new(BadRequest, "-10", $"Document {docEntry} is closed or cancelled and cannot be paid");

    public static ServiceLayerException PaymentExceedsOpenAmount(int docEntry) =>
        new(BadRequest, "-10", $"The sum applied exceeds the open amount of document {docEntry}");

    public static ServiceLayerException PaymentExceedsInstallment(int installmentId) =>
        new(BadRequest, "-10", $"The sum applied exceeds the open amount of installment {installmentId}");

    public static ServiceLayerException AppliedExceedsPayment() =>
        new(BadRequest, "-10", "The sums applied to documents exceed the payment amount");

    public static ServiceLayerException WrongInvoiceType(string expected) =>
        new(BadRequest, "-10", $"This payment can only be applied to documents of type '{expected}'");

    public static ServiceLayerException CashAccountRequired() =>
        new(BadRequest, "-10", "No cash account was given and no default cash account is defined");

    public static ServiceLayerException AccountNotPostable(string code) =>
        new(BadRequest, "-10", $"Account '{code}' is not an active account");

    public static ServiceLayerException ObjectInUse() =>
        new(BadRequest, "-10", "The object is used by other documents and cannot be removed");

    public static ServiceLayerException BrokenReference() =>
        new(BadRequest, "-10", "A value refers to an object that does not exist");

    public static ServiceLayerException NumberOutOfRange() =>
        new(BadRequest, "-10", "A number is too large to be stored");

    /// <summary>Code and format from guide p.154-157. Reasons taken from there are the guide's own words.</summary>
    public static ServiceLayerException SqlSyntax(string sql, int position, string reason) =>
        new(BadRequest, "701", $"Invalid SQL syntax:{sql}, line 1, character position {position}, {reason}");

    /// <summary>Code from the guide. The reason is PostgreSQL's text, so it differs from real SAP.</summary>
    public static ServiceLayerException SqlCannotRun(string reason) =>
        new(BadRequest, "701", $"Invalid SQL syntax: {reason}");

    /// <summary>Guide p.154.</summary>
    public static ServiceLayerException SqlTableNotAccessible(string table) =>
        new(BadRequest, "702", $"Table '{table}' not accessible");

    /// <summary>Guide p.148, where it is about excluded columns. The mock also uses it for unknown ones.</summary>
    public static ServiceLayerException SqlColumnNotAccessible(string column, string table) =>
        new(BadRequest, "703", $"Column '{column}' from table '{table}' not accessible");

    /// <summary>Guide p.160.</summary>
    public static ServiceLayerException SqlParameterError() =>
        new(BadRequest, "704", "Parameter error.");

    /// <summary>Guide p.158.</summary>
    public static ServiceLayerException NotPermitted() =>
        new(Forbidden, "-6006", "You are not permitted to perform this action");

    public static ServiceLayerException InvalidUserFieldName(string name) =>
        new(BadRequest, "-10", $"'{name}' is not a valid field name: use letters, digits and underscores, starting with a letter");

    public static ServiceLayerException UserFieldTableNotSupported(string table) =>
        new(BadRequest, "-10", $"User-defined fields cannot be added to table '{table}'");

    public static ServiceLayerException UserFieldAlreadyExists(string table, string name) =>
        new(BadRequest, "-10", $"Field '{name}' already exists in table '{table}'");

    public static ServiceLayerException UserFieldSizeOutOfRange(int largest) =>
        new(BadRequest, "-10", $"The size of a text field must be between 1 and {largest}");

    public static ServiceLayerException UserFieldCannotShrink(int current) =>
        new(BadRequest, "-10", $"The size cannot be smaller than the current size {current}");

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
