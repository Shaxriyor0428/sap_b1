using System.Globalization;

namespace MockSl.Application.Entities;

/// <summary>One row read from the database, addressed by column name.</summary>
public sealed class EntityRow(IReadOnlyDictionary<string, object?> columns)
{
    public object? this[string column] => columns[column];

    public string? Text(string column) => (string?)columns[column];

    public int Int(string column) => Convert.ToInt32(columns[column], CultureInfo.InvariantCulture);

    public decimal Decimal(string column) => Convert.ToDecimal(columns[column], CultureInfo.InvariantCulture);
}
