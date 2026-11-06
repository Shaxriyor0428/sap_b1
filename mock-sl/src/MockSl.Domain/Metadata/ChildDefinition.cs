namespace MockSl.Domain.Metadata;

/// <summary>
/// A collection inside an object, stored in its own table, for example DocumentLines in INV1.
/// In the API it looks like an ordinary array field of the parent.
/// </summary>
public sealed class ChildDefinition
{
    private readonly Dictionary<string, FieldDefinition> _fieldsByName;

    public ChildDefinition(
        string name,
        string typeName,
        string table,
        string parentKeyColumn,
        string rowKeyField,
        int firstRowKey,
        IReadOnlyList<FieldDefinition> fields,
        bool isReadOnly = false)
    {
        Name = name;
        TypeName = typeName;
        Table = table;
        ParentKeyColumn = parentKeyColumn;
        FirstRowKey = firstRowKey;
        Fields = fields;
        IsReadOnly = isReadOnly;
        _fieldsByName = fields.ToDictionary(field => field.Name);
        RowKey = _fieldsByName[rowKeyField];
    }

    /// <summary>The name of the array in JSON, for example DocumentLines.</summary>
    public string Name { get; }

    /// <summary>The name used in error messages, for example DocumentLine.</summary>
    public string TypeName { get; }

    public string Table { get; }

    /// <summary>The column that points to the parent. Usually DocEntry, but RCT2 uses DocNum.</summary>
    public string ParentKeyColumn { get; }

    /// <summary>Identifies a row within one parent, for example LineNum.</summary>
    public FieldDefinition RowKey { get; }

    /// <summary>Rows are numbered from here: LineNum starts at 0 (guide p.25), InstlmntID at 1 (SDK default).</summary>
    public int FirstRowKey { get; }

    public IReadOnlyList<FieldDefinition> Fields { get; }

    /// <summary>The server maintains these rows, so they are ignored in request bodies.</summary>
    public bool IsReadOnly { get; }

    public FieldDefinition? FindField(string name) => _fieldsByName.GetValueOrDefault(name);

    /// <summary>For fields that must exist because the rules are written against them.</summary>
    public FieldDefinition Field(string name) => _fieldsByName[name];
}
