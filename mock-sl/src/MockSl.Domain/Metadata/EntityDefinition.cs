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
        Func<string, string>? duplicateKeyMessage = null,
        IReadOnlyList<ChildDefinition>? children = null,
        bool canDelete = true,
        IReadOnlyList<NavigationDefinition>? navigations = null)
    {
        SetName = setName;
        TypeName = typeName;
        Table = table;
        Fields = fields;
        Children = children ?? [];
        CanDelete = canDelete;
        Navigations = navigations ?? [];
        DuplicateKeyMessage = duplicateKeyMessage ?? (key => $"{typeName} '{key}' already exists");
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

    public IReadOnlyList<ChildDefinition> Children { get; }

    /// <summary>Sales and purchase documents cannot be deleted, only cancelled or closed (guide p.27-28).</summary>
    public bool CanDelete { get; }

    public IReadOnlyList<NavigationDefinition> Navigations { get; }

    /// <summary>Builds the -10 error text from the key value.</summary>
    public Func<string, string> DuplicateKeyMessage { get; }

    public FieldDefinition? FindField(string name) => _fieldsByName.GetValueOrDefault(name);

    public ChildDefinition? FindChild(string name) => Children.FirstOrDefault(child => child.Name == name);

    public NavigationDefinition? FindNavigation(string name) => Navigations.FirstOrDefault(navigation => navigation.Name == name);

    /// <summary>For fields that must exist because the rules are written against them.</summary>
    public FieldDefinition Field(string name) => _fieldsByName[name];

    /// <summary>For collections that must exist because the rules are written against them.</summary>
    public ChildDefinition Child(string name) => Children.First(child => child.Name == name);

    /// <summary>
    /// A copy that also knows the user-defined fields of its table, and of the objects it links to.
    /// Definitions are fixed once built, so a field created at run time needs a new copy.
    /// </summary>
    public EntityDefinition WithUserFields(ILookup<string, FieldDefinition> userFieldsByTable) => new(
        SetName,
        TypeName,
        Table,
        Key.Select(field => field.Name).ToList(),
        [.. Fields, .. userFieldsByTable[Table]],
        DuplicateKeyMessage,
        Children,
        CanDelete,
        Navigations.Select(navigation => navigation with { Target = navigation.Target.WithUserFields(userFieldsByTable) }).ToList());
}
