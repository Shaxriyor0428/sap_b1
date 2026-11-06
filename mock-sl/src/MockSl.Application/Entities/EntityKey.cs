using System.Globalization;
using MockSl.Application.OData;
using MockSl.Domain.Errors;
using MockSl.Domain.Metadata;

namespace MockSl.Application.Entities;

public static class EntityKey
{
    /// <summary>Matches URL key parts to the key fields of the object and converts their values.</summary>
    public static IReadOnlyList<FieldValue> Bind(EntityDefinition entity, IReadOnlyList<KeyPart> parts)
    {
        if (parts.Count != entity.Key.Count)
        {
            throw SapErrors.UnrecognizedResource();
        }

        var values = new List<FieldValue>();
        for (var index = 0; index < parts.Count; index++)
        {
            var field = parts[index].Name is { } name
                ? entity.Key.FirstOrDefault(keyField => keyField.Name == name)
                : entity.Key.Count == 1 ? entity.Key[0] : null;

            if (field is null)
            {
                throw SapErrors.UnrecognizedResource();
            }

            values.Add(new FieldValue(field, LiteralConverter.Convert(field, parts[index].Value)));
        }

        return values;
    }

    /// <summary>Writes the key part of a URL from a stored row: ('C0001'), or (A='x',B=1) for several fields.</summary>
    public static string Format(EntityDefinition entity, IReadOnlyDictionary<string, object?> row)
    {
        if (entity.Key.Count == 1)
        {
            return $"({Literal(row[entity.Key[0].Column])})";
        }

        var parts = entity.Key.Select(field => $"{field.Name}={Literal(row[field.Column])}");
        return $"({string.Join(',', parts)})";
    }

    /// <summary>The key value as shown to a user, for messages.</summary>
    public static string Describe(IEnumerable<FieldValue> values, EntityDefinition entity)
    {
        var keyValues = entity.Key.Select(field => values.FirstOrDefault(value => value.Field == field)?.Value);
        return string.Join(", ", keyValues);
    }

    private static string Literal(object? value) => value switch
    {
        string text => $"'{text.Replace("'", "''")}'",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
    };
}
