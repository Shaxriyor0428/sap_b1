using System.Globalization;
using MockSl.Domain.Errors;
using MockSl.Domain.Metadata;

namespace MockSl.Application.OData;

/// <summary>
/// Turns a literal from a URL into the value stored in the database:
/// cCustomer becomes 'C', '2026-11-01' becomes a date, and so on.
/// </summary>
public static class LiteralConverter
{
    public static object Convert(FieldDefinition field, Token literal) => (field.Type, literal.Kind) switch
    {
        (FieldType.Text, TokenKind.String) => literal.Text,
        (FieldType.Enumeration, TokenKind.String) => ToEnumDbValue(field, literal.Text),
        (FieldType.Date, TokenKind.String) => ToDate(field, literal.Text),
        (FieldType.Integer, TokenKind.Number) => ToInteger(field, literal.Text),
        (FieldType.Decimal, TokenKind.Number) => ToDecimal(field, literal.Text),
        _ => throw SapErrors.InvalidValue(field.Name, literal.Text),
    };

    public static string ToEnumDbValue(FieldDefinition field, string text) =>
        field.Enum!.TryGetDbValue(text, out var dbValue)
            ? dbValue
            : throw SapErrors.InvalidValue(field.Name, text);

    public static DateOnly ToDate(FieldDefinition field, string text) =>
        SapDate.TryParse(text, out var date)
            ? date
            : throw SapErrors.InvalidValue(field.Name, text);

    public static int ToInteger(FieldDefinition field, string text) =>
        int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)
            ? number
            : throw SapErrors.InvalidValue(field.Name, text);

    public static decimal ToDecimal(FieldDefinition field, string text) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
            ? number
            : throw SapErrors.InvalidValue(field.Name, text);
}
