using System.Globalization;
using MockSl.Domain.Metadata;

namespace MockSl.Infrastructure.Persistence;

/// <summary>
/// DDL for user-defined fields. DDL cannot use parameters, so names are placed in the text.
/// UserFieldPolicy has validated them before this point, and they are quoted here as well.
/// </summary>
public static class UserFieldSqlBuilder
{
    public static string AddColumn(string table, FieldDefinition column) =>
        $"ALTER TABLE {SqlNames.Quote(table)} ADD COLUMN {SqlNames.Quote(column.Column)} {ColumnType(column)}";

    public static string ResizeColumn(string table, string column, int size) =>
        $"ALTER TABLE {SqlNames.Quote(table)} ALTER COLUMN {SqlNames.Quote(column)} TYPE {TextType(size)}";

    public static string DropColumn(string table, string column) =>
        $"ALTER TABLE {SqlNames.Quote(table)} DROP COLUMN {SqlNames.Quote(column)}";

    private static string ColumnType(FieldDefinition column) => column.Type switch
    {
        FieldType.Text when column.MaxLength is { } length => TextType(length),
        FieldType.Text => "TEXT",
        FieldType.Integer => "INT",
        FieldType.Date => "DATE",
        FieldType.Decimal => "NUMERIC(19, 6)",
        _ => throw new InvalidOperationException($"Unhandled field type {column.Type}"),
    };

    private static string TextType(int length) => $"VARCHAR({length.ToString(CultureInfo.InvariantCulture)})";
}
