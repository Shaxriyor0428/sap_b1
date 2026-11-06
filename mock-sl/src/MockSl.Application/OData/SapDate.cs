using System.Globalization;

namespace MockSl.Application.OData;

/// <summary>Reads the date formats listed in the guide (p.34). Hours and minutes are ignored.</summary>
public static class SapDate
{
    private static readonly string[] Formats =
    [
        "yyyy-MM-dd",
        "yyyyMMdd",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm:ssZ",
        "yyyyMMddHHmmss",
    ];

    public static bool TryParse(string text, out DateOnly date)
    {
        var isValid = DateTime.TryParseExact(
            text,
            Formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed);

        date = DateOnly.FromDateTime(parsed);
        return isValid;
    }

    /// <summary>
    /// The EDMX types dates as Edm.DateTimeOffset, so v2 output uses this shape.
    /// No v2 sample exists in the guide, see open question 1 in docs/sl-contract.md.
    /// </summary>
    public static string Format(DateOnly date) =>
        $"{date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}T00:00:00Z";
}
