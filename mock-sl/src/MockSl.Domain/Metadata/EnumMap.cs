using System.Diagnostics.CodeAnalysis;

namespace MockSl.Domain.Metadata;

/// <summary>
/// Translates an enum between its Service Layer name and its database value,
/// for example cCustomer and 'C'.
/// </summary>
public sealed class EnumMap
{
    private readonly Dictionary<string, string> _dbValueByName = [];
    private readonly Dictionary<string, string> _nameByDbValue = [];

    public EnumMap(params (string Name, string DbValue)[] members)
    {
        foreach (var (name, dbValue) in members)
        {
            _dbValueByName.Add(name, dbValue);
            _nameByDbValue.Add(dbValue, name);
        }
    }

    /// <summary>The guide (p.24, p.33) says both the name and the value are accepted as input.</summary>
    public bool TryGetDbValue(string nameOrDbValue, [NotNullWhen(true)] out string? dbValue)
    {
        if (_dbValueByName.TryGetValue(nameOrDbValue, out dbValue))
        {
            return true;
        }

        dbValue = _nameByDbValue.ContainsKey(nameOrDbValue) ? nameOrDbValue : null;
        return dbValue is not null;
    }

    public string GetName(string dbValue) => _nameByDbValue[dbValue];
}
