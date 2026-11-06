namespace MockSl.Domain.Metadata;

/// <summary>
/// Describes one Service Layer object. The generic handler reads this description,
/// so adding an object means adding a definition, not a new handler.
/// </summary>
public sealed class EntityDefinition
{
    private readonly Dictionary<string, FieldDefinition> _fieldsByName;

    public EntityDefinition(
        string setName,
        string typeName,
        string table,
        IReadOnlyList<string> keyFields,
        IReadOnlyList<FieldDefinition> fields,
        Func<string, string> duplicateKeyMessage)
    {
        SetName = setName;
        TypeName = typeName;
        Table = table;
        Fields = fields;
        DuplicateKeyMessage = duplicateKeyMessage;
        _fieldsByName = fields.ToDictionary(field => field.Name);
        Key = keyFields.Select(name => _fieldsByName[name]).ToList();
    }

    /// <summary>The name used in URLs, for example BusinessPartners.</summary>
    public string SetName { get; }

    /// <summary>The name used in error messages, for example BusinessPartner.</summary>
    public string TypeName { get; }

    public string Table { get; }

    public IReadOnlyList<FieldDefinition> Key { get; }

    public IReadOnlyList<FieldDefinition> Fields { get; }

    /// <summary>Builds the -10 error text from the key value.</summary>
    public Func<string, string> DuplicateKeyMessage { get; }

    public FieldDefinition? FindField(string name) => _fieldsByName.GetValueOrDefault(name);
}
