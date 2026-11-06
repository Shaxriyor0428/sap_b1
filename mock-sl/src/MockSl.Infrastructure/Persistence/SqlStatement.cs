namespace MockSl.Infrastructure.Persistence;

public sealed record SqlParameterValue(string Name, object? Value);

/// <summary>SQL text plus its parameters. User values only ever live in Parameters.</summary>
public sealed record SqlStatement(string Text, IReadOnlyList<SqlParameterValue> Parameters);

/// <summary>Hands out parameter names (@p0, @p1, ...) while a statement is being built.</summary>
public sealed class ParameterBag
{
    private readonly List<SqlParameterValue> _values = [];

    public IReadOnlyList<SqlParameterValue> Values => _values;

    public string Add(object? value)
    {
        var name = $"@p{_values.Count}";
        _values.Add(new SqlParameterValue(name, value));
        return name;
    }
}
