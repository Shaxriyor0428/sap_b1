using System.Globalization;
using MockSl.Domain.Errors;
using MockSl.Domain.Metadata;

namespace MockSl.Application.OData;

public sealed record OrderByItem(FieldDefinition Field, bool Descending);

/// <summary>One linked object to attach to each row, and which of its fields to show.</summary>
public sealed record ExpandItem(NavigationDefinition Navigation, IReadOnlyList<FieldDefinition> Select);

/// <summary>The query options of a request, validated against the object definition.</summary>
public sealed record ODataQuery(
    IReadOnlyList<FieldDefinition> Select,
    IReadOnlyList<ChildDefinition> Children,
    IReadOnlyList<ExpandItem> Expand,
    FilterNode? Filter,
    IReadOnlyList<OrderByItem> OrderBy,
    int? Top,
    int Skip)
{
    private static readonly HashSet<string> SupportedOptions = ["$select", "$filter", "$orderby", "$top", "$skip", "$expand"];

    public static ODataQuery Parse(EntityDefinition entity, IReadOnlyDictionary<string, string> options)
    {
        foreach (var name in options.Keys)
        {
            if (name.StartsWith('$') && !SupportedOptions.Contains(name))
            {
                throw SapErrors.UnsupportedQueryOption(name);
            }
        }

        var expand = options.TryGetValue("$expand", out var expandText) ? ExpandParser.Parse(entity, expandText) : [];
        var selection = ParseSelect(entity, options.GetValueOrDefault("$select"), expand);

        return new ODataQuery(
            selection.Fields,
            selection.Children,
            ToExpandItems(expand, selection.ExpandPaths),
            options.TryGetValue("$filter", out var filter) ? FilterParser.Parse(entity, filter) : null,
            ParseOrderBy(entity, options.GetValueOrDefault("$orderby")),
            options.TryGetValue("$top", out var top) ? ParseCount("$top", top) : null,
            options.TryGetValue("$skip", out var skip) ? ParseCount("$skip", skip) : 0);
    }

    private static Selection ParseSelect(EntityDefinition entity, string? select, IReadOnlyList<ExpandRequest> expand)
    {
        var selection = new Selection();

        if (string.IsNullOrWhiteSpace(select))
        {
            selection.Fields.AddRange(entity.Fields);
            selection.Children.AddRange(entity.Children);
            return selection;
        }

        foreach (var name in SplitList(select))
        {
            if (name == "*")
            {
                selection.Fields.AddRange(entity.Fields);
                selection.Children.AddRange(entity.Children);
            }
            else if (name.Contains('/'))
            {
                AddNavigationField(entity, name, expand, selection);
            }
            else if (entity.FindChild(name) is { } child)
            {
                selection.Children.Add(child);
            }
            else if (entity.FindNavigation(name) is null)
            {
                // A link named here is not a field of its own: $expand decides whether it is shown.
                selection.Fields.Add(entity.FindField(name) ?? throw SapErrors.InvalidProperty(entity.TypeName, name));
            }
        }

        return selection;
    }

    /// <summary>
    /// The other way to choose linked fields: $select=BusinessPartner/CardName.
    /// Without a matching $expand the guide says it is ignored (p.83).
    /// </summary>
    private static void AddNavigationField(
        EntityDefinition entity,
        string path,
        IReadOnlyList<ExpandRequest> expand,
        Selection selection)
    {
        var parts = path.Split('/');
        var navigation = (parts.Length == 2 ? entity.FindNavigation(parts[0]) : null)
            ?? throw SapErrors.InvalidProperty(entity.TypeName, path);

        if (expand.All(request => request.Navigation.Name != navigation.Name))
        {
            return;
        }

        var field = navigation.Target.FindField(parts[1])
            ?? throw SapErrors.InvalidProperty(navigation.Target.TypeName, parts[1]);

        selection.ExpandPaths.Add((navigation.Name, field));
    }

    private static List<ExpandItem> ToExpandItems(
        IReadOnlyList<ExpandRequest> expand,
        List<(string Navigation, FieldDefinition Field)> paths) =>
        expand.Select(request =>
        {
            var fields = (request.Select ?? [])
                .Concat(paths.Where(path => path.Navigation == request.Navigation.Name).Select(path => path.Field))
                .Distinct()
                .ToList();

            // Nothing chosen means every field of the linked object (guide p.83).
            return new ExpandItem(request.Navigation, fields.Count > 0 ? fields : request.Navigation.Target.Fields);
        }).ToList();

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

    private sealed class Selection
    {
        public List<FieldDefinition> Fields { get; } = [];

        public List<ChildDefinition> Children { get; } = [];

        public List<(string Navigation, FieldDefinition Field)> ExpandPaths { get; } = [];
    }
}
