namespace MockSl.Domain.Metadata;

/// <summary>
/// One field of an object: its Service Layer name and the database column behind it.
/// The two can differ, for example EmailAddress and E_Mail.
/// </summary>
public sealed record FieldDefinition
{
    public required string Name { get; init; }

    public required string Column { get; init; }

    public required FieldType Type { get; init; }

    public EnumMap? Enum { get; init; }

    public int? MaxLength { get; init; }

    /// <summary>Read-only fields are silently ignored in request bodies (guide p.27).</summary>
    public bool IsReadOnly { get; init; }

    public bool IsRequired { get; init; }

    public static FieldDefinition Text(string name, int maxLength, string? column = null) =>
        new() { Name = name, Column = column ?? name, Type = FieldType.Text, MaxLength = maxLength };

    public static FieldDefinition Decimal(string name, string? column = null) =>
        new() { Name = name, Column = column ?? name, Type = FieldType.Decimal };

    public static FieldDefinition Enumeration(string name, EnumMap map, string? column = null) =>
        new() { Name = name, Column = column ?? name, Type = FieldType.Enumeration, Enum = map };
}
