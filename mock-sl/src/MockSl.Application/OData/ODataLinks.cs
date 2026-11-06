namespace MockSl.Application.OData;

public static class ODataLinks
{
    /// <summary>Keeps the client's options and replaces only $top and $skip.</summary>
    public static string BuildNextLink(
        string resource,
        IEnumerable<KeyValuePair<string, string>> options,
        NextPage next)
    {
        var parts = options
            .Where(option => option.Key is not ("$top" or "$skip"))
            .Select(option => $"{option.Key}={Uri.EscapeDataString(option.Value)}")
            .ToList();

        if (next.Top is { } top)
        {
            parts.Add($"$top={top}");
        }

        parts.Add($"$skip={next.Skip}");
        return $"{resource}?{string.Join('&', parts)}";
    }
}
