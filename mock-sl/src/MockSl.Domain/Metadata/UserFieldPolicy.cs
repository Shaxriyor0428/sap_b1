using MockSl.Domain.Errors;

namespace MockSl.Domain.Metadata;

/// <summary>
/// The rules of user-defined fields. A new field means ALTER TABLE, and DDL cannot use parameters,
/// so the table and the name must be proven safe here before they reach any SQL.
/// </summary>
public static class UserFieldPolicy
{
    /// <summary>All user-defined fields are prefixed with U_ (guide p.86). The client sends the name without it.</summary>
    public const string ColumnPrefix = "U_";

    private const string TextType = "A";
    private const string MemoType = "M";
    private const string NumericType = "N";
    private const string DateType = "D";

    private const int LargestTextSize = 254;

    public static void EnsureValidName(string name)
    {
        var isValid = name.Length > 0
            && char.IsAsciiLetter(name[0])
            && name.All(character => char.IsAsciiLetterOrDigit(character) || character == '_');

        if (!isValid)
        {
            throw SapErrors.InvalidUserFieldName(name);
        }
    }

    public static void EnsureAllowedTable(string table)
    {
        if (!EntityCatalog.TablesWithUserFields.Contains(table))
        {
            throw SapErrors.UserFieldTableNotSupported(table);
        }
    }

    /// <summary>FieldID counts per table, starting at 0 (guide p.87).</summary>
    public static int NextFieldId(IEnumerable<int> existing) => existing.Select(id => id + 1).DefaultIfEmpty(0).Max();

    /// <summary>
    /// The SDK says Size follows EditSize and should not be set, while the guide's sample sends Size (p.87).
    /// Both are accepted. Only text fields need a size, because it becomes the length of the column.
    /// </summary>
    public static int ResolveEditSize(string fieldType, int? editSize, int? size)
    {
        var resolved = editSize ?? size;

        if (fieldType != TextType)
        {
            return resolved ?? 0;
        }

        return resolved is >= 1 and <= LargestTextSize
            ? resolved.Value
            : throw SapErrors.UserFieldSizeOutOfRange(LargestTextSize);
    }

    public static void EnsureCanResize(string fieldType, int current, int requested)
    {
        if (fieldType != TextType)
        {
            return;
        }

        if (requested > LargestTextSize)
        {
            throw SapErrors.UserFieldSizeOutOfRange(LargestTextSize);
        }

        // A shorter column could not hold the values already stored in it.
        if (requested < current)
        {
            throw SapErrors.UserFieldCannotShrink(current);
        }
    }

    public static bool IsText(string fieldType) => fieldType == TextType;

    /// <summary>How the field looks to the rest of the system: an ordinary field of its object.</summary>
    public static FieldDefinition ToField(string name, string fieldType, int editSize)
    {
        var fieldName = $"{ColumnPrefix}{name}";

        return fieldType switch
        {
            TextType => FieldDefinition.Text(fieldName, editSize),
            MemoType => new FieldDefinition { Name = fieldName, Column = fieldName, Type = FieldType.Text },
            NumericType => FieldDefinition.Integer(fieldName),
            DateType => FieldDefinition.Date(fieldName),
            _ => FieldDefinition.Decimal(fieldName),
        };
    }
}
