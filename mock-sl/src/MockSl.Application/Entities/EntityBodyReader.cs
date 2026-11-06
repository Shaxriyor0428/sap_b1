using System.Text.Json;
using MockSl.Application.OData;
using MockSl.Domain.Errors;
using MockSl.Domain.Metadata;

namespace MockSl.Application.Entities;

/// <summary>Validates a JSON request body against the object definition and converts it to database values.</summary>
public static class EntityBodyReader
{
    public static EntityDraft ReadForCreate(EntityDefinition entity, JsonElement body)
    {
        EnsureObject(body);
        var draft = new EntityDraft();

        foreach (var property in body.EnumerateObject())
        {
            // A client may send back an object it received, including @odata.context.
            if (property.Name.StartsWith('@'))
            {
                continue;
            }

            if (entity.FindChild(property.Name) is { } child)
            {
                if (!child.IsReadOnly)
                {
                    draft.SetRows(child, ReadChildRows(child, property.Value));
                }

                continue;
            }

            var field = entity.FindField(property.Name)
                ?? throw SapErrors.InvalidProperty(entity.TypeName, property.Name);

            // Guide p.27: read-only properties are ignored silently.
            if (!field.IsReadOnly)
            {
                draft.Header.Set(field, Convert(entity.TypeName, field, property.Value));
            }
        }

        EnsureRequired(entity.TypeName, entity.Fields, draft.Header);
        return draft;
    }

    public static DraftRow ReadForUpdate(EntityDefinition entity, JsonElement body)
    {
        EnsureObject(body);
        var changes = new DraftRow();

        foreach (var property in body.EnumerateObject())
        {
            // Collections are fixed once a document exists, so they count as read-only here.
            if (property.Name.StartsWith('@') || entity.FindChild(property.Name) is not null)
            {
                continue;
            }

            var field = entity.FindField(property.Name)
                ?? throw SapErrors.InvalidProperty(entity.TypeName, property.Name);

            if (!field.IsReadOnly && !field.IsCreateOnly && !entity.Key.Contains(field))
            {
                changes.Set(field, Convert(entity.TypeName, field, property.Value));
            }
        }

        return changes;
    }

    private static List<DraftRow> ReadChildRows(ChildDefinition child, JsonElement json)
    {
        if (json.ValueKind == JsonValueKind.Null)
        {
            return [];
        }

        if (json.ValueKind != JsonValueKind.Array)
        {
            throw SapErrors.InvalidValue(child.Name, json.GetRawText());
        }

        return json.EnumerateArray().Select(element => ReadChildRow(child, element)).ToList();
    }

    private static DraftRow ReadChildRow(ChildDefinition child, JsonElement json)
    {
        EnsureObject(json);
        var row = new DraftRow();

        foreach (var property in json.EnumerateObject())
        {
            var field = child.FindField(property.Name)
                ?? throw SapErrors.InvalidProperty(child.TypeName, property.Name);

            if (!field.IsReadOnly)
            {
                row.Set(field, Convert(child.TypeName, field, property.Value));
            }
        }

        EnsureRequired(child.TypeName, child.Fields, row);
        return row;
    }

    private static void EnsureObject(JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Object)
        {
            throw SapErrors.InvalidRequestBody();
        }
    }

    private static void EnsureRequired(string typeName, IEnumerable<FieldDefinition> fields, DraftRow row)
    {
        foreach (var field in fields.Where(field => field.IsRequired))
        {
            if (row.Get(field.Column) is null or "")
            {
                throw SapErrors.RequiredProperty(typeName, field.Name);
            }
        }
    }

    private static object? Convert(string typeName, FieldDefinition field, JsonElement json)
    {
        if (json.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return field.Type switch
        {
            FieldType.Text => ReadText(typeName, field, json),
            FieldType.Enumeration => LiteralConverter.ToEnumDbValue(field, ReadString(field, json)),
            FieldType.Date => LiteralConverter.ToDate(field, ReadString(field, json)),
            FieldType.Integer => LiteralConverter.ToInteger(field, ReadNumberText(field, json)),
            FieldType.Decimal => LiteralConverter.ToDecimal(field, ReadNumberText(field, json)),
            _ => throw new InvalidOperationException($"Unhandled field type {field.Type}"),
        };
    }

    private static string ReadText(string typeName, FieldDefinition field, JsonElement json)
    {
        var text = ReadString(field, json);
        return field.MaxLength is { } maxLength && text.Length > maxLength
            ? throw SapErrors.ValueTooLong(typeName, field.Name)
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
