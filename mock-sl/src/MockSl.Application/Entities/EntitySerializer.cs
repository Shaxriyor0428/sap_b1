using System.Text.Json.Nodes;
using MockSl.Application.OData;
using MockSl.Domain.Metadata;

namespace MockSl.Application.Entities;

/// <summary>Turns database rows into the JSON the client sees: Service Layer names and enum names.</summary>
public static class EntitySerializer
{
    public static JsonObject ToJson(IEnumerable<FieldDefinition> fields, EntityRow row)
    {
        var json = new JsonObject();

        foreach (var field in fields)
        {
            json[field.Name] = ToJsonValue(field, row[field.Column]);
        }

        return json;
    }

    public static JsonArray ToJsonArray(ChildDefinition child, IEnumerable<EntityRow> rows)
    {
        var array = new JsonArray();

        foreach (var row in rows)
        {
            array.Add(ToJson(child.Fields, row));
        }

        return array;
    }

    private static JsonNode? ToJsonValue(FieldDefinition field, object? value) => value switch
    {
        null => null,
        string text when field.Type == FieldType.Enumeration => JsonValue.Create(field.Enum!.GetName(text)),
        string text => JsonValue.Create(text),
        int number => JsonValue.Create(number),
        decimal number => JsonValue.Create((double)number),
        DateTime date => JsonValue.Create(SapDate.Format(DateOnly.FromDateTime(date))),
        DateOnly date => JsonValue.Create(SapDate.Format(date)),
        _ => throw new InvalidOperationException($"Unhandled value type {value.GetType()} for {field.Name}"),
    };
}
