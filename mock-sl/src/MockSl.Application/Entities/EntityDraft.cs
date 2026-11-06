using MockSl.Domain.Metadata;

namespace MockSl.Application.Entities;

/// <summary>Values waiting to be written to one row. The rules read and complete them first.</summary>
public sealed class DraftRow
{
    private readonly List<FieldValue> _values = [];

    public IReadOnlyList<FieldValue> Values => _values;

    public void Set(FieldDefinition field, object? value)
    {
        // Two API names can share a column, such as Price and UnitPrice. The last value wins,
        // because the same column cannot appear twice in an INSERT or UPDATE.
        _values.RemoveAll(existing => existing.Field.Column == field.Column);
        _values.Add(new FieldValue(field, value));
    }

    public object? Get(string column) => _values.FirstOrDefault(value => value.Field.Column == column)?.Value;

    public string? Text(string column) => Get(column) as string;

    public int? Int(string column) => Get(column) as int?;

    public decimal? Decimal(string column) => Get(column) as decimal?;

    public DateOnly? Date(string column) => Get(column) as DateOnly?;
}

/// <summary>An object being created: its header row and the rows of its collections.</summary>
public sealed class EntityDraft
{
    private readonly Dictionary<ChildDefinition, IReadOnlyList<DraftRow>> _children = [];

    public DraftRow Header { get; } = new();

    public IEnumerable<ChildDefinition> ChildrenWithRows => _children.Keys;

    public IReadOnlyList<DraftRow> Rows(ChildDefinition child) => _children.GetValueOrDefault(child) ?? [];

    public void SetRows(ChildDefinition child, IReadOnlyList<DraftRow> rows) => _children[child] = rows;
}
