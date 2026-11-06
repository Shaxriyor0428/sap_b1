using System.Globalization;

namespace MockSl.Infrastructure.Persistence;

public static class SqlPaging
{
    /// <summary>
    /// Pages any checked query from outside, the way guide p.145 shows for Microsoft SQL.
    /// The query may hold its own LIMIT from TOP, which is why the page cannot just be added to its end.
    /// Limit and offset are numbers made by the server, not text from the request.
    /// </summary>
    public static string Wrap(string executable, int? limit, int offset)
    {
        var sql = $"SELECT * FROM ({executable}) B1S_DUMMY_TABLE";

        if (limit is { } rows)
        {
            sql += $" LIMIT {rows.ToString(CultureInfo.InvariantCulture)}";
        }

        return $"{sql} OFFSET {offset.ToString(CultureInfo.InvariantCulture)}";
    }
}
