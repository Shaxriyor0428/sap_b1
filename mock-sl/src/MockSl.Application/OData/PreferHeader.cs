using System.Globalization;

namespace MockSl.Application.OData;

public static class PreferHeader
{
    private const string MaxPageSizePrefix = "odata.maxpagesize=";

    /// <summary>Reads "Prefer: odata.maxpagesize=50" (guide p.35). Returns null if absent or invalid.</summary>
    public static int? ReadMaxPageSize(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return null;
        }

        foreach (var preference in header.Split(',', StringSplitOptions.TrimEntries))
        {
            if (preference.StartsWith(MaxPageSizePrefix, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(preference[MaxPageSizePrefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var size))
            {
                return size;
            }
        }

        return null;
    }
}
