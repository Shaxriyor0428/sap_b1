using System.Text.Json;
using MockSl.Application.OData;
using MockSl.Domain.Errors;
using MockSl.Domain.Metadata;

namespace MockSl.Application.Entities;

/// <summary>Validates a JSON request body against the object definition and converts it to database values.</summary>
public static class EntityBodyReader
{
    public static IReadOnlyList<FieldValue> ReadForCreate(EntityDefinition entity, JsonElement body)
    {
        var values = Read(entity, body, isCreate: true);

        foreach (var field in entity.Fields.Where(field => field.IsRequired))
        {
            var value = values.FirstOrDefault(candidate => candidate.Field == field)?.Value;
            if (value is null or "")
            {
                throw SapErrors.RequiredProperty(entity.TypeName, field.Name);
            }
        }

        return values;
    }

    public static IReadOnlyList<FieldValue> ReadForUpdate(EntityDefinition entity, JsonElement body) =>
        Read(entity, body, isCreate: false);

    private static List<FieldValue> Read(EntityDefinition entity, JsonElement body, bool isCreate)
    {
        if (body.ValueKind != JsonValueKind.Object)
        {
            throw SapErrors.InvalidRequestBody();
        }

        var values = new List<FieldValue>();

        foreach (var property in body.EnumerateObject())
        {
            // A client may send back an object it received, including @odata.context.
            if (property.Name.StartsWith('@'))
            {
                continue;
            }

            var field = entity.FindField(property.Name)
                ?? throw SapErrors.InvalidProperty(entity.TypeName, property.Name);

            // Guide p.27: read-only properties, such as the key on update, are ignored silently.
            if (field.IsReadOnly || (!isCreate && entity.Key.Contains(field)))
            {
                continue;
            }

            // JSON allows a repeated name. The last one wins, as in a normal JSON object,
            // because the same column cannot appear twice in an INSERT or UPDATE.
            values.RemoveAll(value => value.Field == field);
            values.Add(new FieldValue(field, Convert(entity, field, property.Value)));
        }

        return values;
    }

    private static object? Convert(EntityDefinition entity, FieldDefinition field, JsonElement json)
    {
        if (json.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return field.Type switch
        {
            FieldType.Text => ReadText(entity, field, json),
            FieldType.Enumeration => LiteralConverter.ToEnumDbValue(field, ReadString(field, json)),
            FieldType.Date => LiteralConverter.ToDate(field, ReadString(field, json)),
            FieldType.Integer => LiteralConverter.ToInteger(field, ReadNumberText(field, json)),
            FieldType.Decimal => LiteralConverter.ToDecimal(field, ReadNumberText(field, json)),
            _ => throw new InvalidOperationException($"Unhandled field type {field.Type}"),
        };
    }

    private static string ReadText(EntityDefinition entity, FieldDefinition field, JsonElement json)
    {
        var text = ReadString(field, json);
        return field.MaxLength is { } maxLength && text.Length > maxLength
            ? throw SapErrors.ValueTooLong(entity.TypeName, field.Name)
            : text;
    }

    private static string ReadString(FieldDefinition field, JsonElement json) =>
        json.ValueKind == JsonValueKind.String
            ? json.GetString()!
            : throw SapErrors.InvalidValue(field.Name, json.GetRawText());

    // SAP samples send numbers both as 100 and "100" (B1 API Cookbook, PurchaseInvoices).
    private static string ReadNumberText(FieldDefinition field, JsonElement json) => json.ValueKind switch
    {
        JsonValueKind.Number => json.GetRawText(),
        JsonValueKind.String => json.GetString()!,
        _ => throw SapErrors.InvalidValue(field.Name, json.GetRawText()),
    };
}
