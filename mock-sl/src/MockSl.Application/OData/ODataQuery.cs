using System.Globalization;
using MockSl.Domain.Errors;
using MockSl.Domain.Metadata;

namespace MockSl.Application.OData;

public sealed record OrderByItem(FieldDefinition Field, bool Descending);

/// <summary>The query options of a request, validated against the object definition.</summary>
public sealed record ODataQuery(
    IReadOnlyList<FieldDefinition> Select,
    FilterNode? Filter,
    IReadOnlyList<OrderByItem> OrderBy,
    int? Top,
    int Skip)
{
    private static readonly HashSet<string> SupportedOptions = ["$select", "$filter", "$orderby", "$top", "$skip"];

    public static ODataQuery Parse(EntityDefinition entity, IReadOnlyDictionary<string, string> options)
    {
        foreach (var name in options.Keys)
        {
            if (name.StartsWith('$') && !SupportedOptions.Contains(name))
            {
                throw SapErrors.UnsupportedQueryOption(name);
            }
        }

        return new ODataQuery(
            ParseSelect(entity, options.GetValueOrDefault("$select")),
            options.TryGetValue("$filter", out var filter) ? FilterParser.Parse(entity, filter) : null,
            ParseOrderBy(entity, options.GetValueOrDefault("$orderby")),
            options.TryGetValue("$top", out var top) ? ParseCount("$top", top) : null,
            options.TryGetValue("$skip", out var skip) ? ParseCount("$skip", skip) : 0);
    }

    private static IReadOnlyList<FieldDefinition> ParseSelect(EntityDefinition entity, string? select)
    {
        if (string.IsNullOrWhiteSpace(select) || select.Trim() == "*")
        {
            return entity.Fields;
        }

        return SplitList(select)
            .Select(name => entity.FindField(name) ?? throw SapErrors.InvalidProperty(entity.TypeName, name))
            .ToList();
    }

    private static IReadOnlyList<OrderByItem> ParseOrderBy(EntityDefinition entity, string? orderBy)
    {
        if (string.IsNullOrWhiteSpace(orderBy))
        {
            return [];
        }

        return SplitList(orderBy).Select(item => ParseOrderByItem(entity, item)).ToList();
    }

    private static OrderByItem ParseOrderByItem(EntityDefinition entity, string item)
    {
        var parts = item.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var field = entity.FindField(parts[0]) ?? throw SapErrors.InvalidProperty(entity.TypeName, parts[0]);

        return parts switch
        {
            [_] or [_, "asc"] => new OrderByItem(field, Descending: false),
            [_, "desc"] => new OrderByItem(field, Descending: true),
            _ => throw SapErrors.InvalidQueryOption("$orderby", item),
        };
    }

    private static int ParseCount(string option, string value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var count)
            ? count
            : throw SapErrors.InvalidQueryOption(option, value);

    private static string[] SplitList(string list) =>
        list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
