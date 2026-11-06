namespace MockSl.Infrastructure.Persistence;

public static class SqlNames
{
    /// <summary>
    /// Quotes a table or column name so PostgreSQL keeps its exact SAP spelling.
    /// Names come from entity definitions, never from the request.
    /// </summary>
    public static string Quote(string name) => $"\"{name.Replace("\"", "\"\"")}\"";
}
