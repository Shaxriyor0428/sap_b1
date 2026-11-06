using System.Globalization;
using System.Text.Json.Nodes;
using MockSl.Application.Entities;
using MockSl.Application.OData;
using MockSl.Domain.Errors;
using MockSl.Domain.Metadata;
using MockSl.Domain.SqlQueries;

namespace MockSl.Application.SqlQueries;

/// <summary>Checked SQL on its way to the database. Values travel apart from the text.</summary>
public sealed record SqlRun(string Executable, IReadOnlyDictionary<string, string> Parameters, int? Limit, int Offset);

/// <summary>One cell of a result. A list keeps the columns in the order of the select list.</summary>
public sealed record SqlCell(string Name, object? Value);

public sealed record SqlRow(IReadOnlyList<SqlCell> Cells);

public sealed record SqlQueryResult(string SqlText, JsonArray Value, string? NextLink);

/// <summary>The List function: runs a saved query (guide p.143-145, p.152-153).</summary>
public sealed class SqlQueryRunner(IEntityStore store)
{
    private static readonly EntityDefinition Definition = EntityCatalog.SqlQueries;

    public async Task<SqlQueryResult> ListAsync(
        IReadOnlyList<FieldValue> key,
        IReadOnlyList<SqlParameter> parameters,
        PagePlan plan,
        CancellationToken cancellationToken)
    {
        var (normalized, keyInUrl) = await ReadQueryAsync(key, cancellationToken);
        EnsureParametersMatch(normalized, parameters);

        var values = parameters.ToDictionary(parameter => parameter.Name, parameter => parameter.Value);
        var run = new SqlRun(normalized.Executable, values, plan.FetchLimit, plan.Skip);
        var rows = await store.RunReadOnlyQueryAsync(run, cancellationToken);

        var visibleRows = plan.RowLimit is { } limit ? rows.Take(limit) : rows;
        var nextLink = plan.NextPageAfter(rows.Count) is { } next
            ? ODataLinks.BuildNextLink(
                $"{Definition.SetName}{keyInUrl}/List",
                parameters.Select(parameter => KeyValuePair.Create(parameter.Name, parameter.Raw)),
                next)
            : null;

        return new SqlQueryResult(normalized.SqlText, ToJson(visibleRows), nextLink);
    }

    // Normalized again on every run: a user-defined field the query reads may have been removed since.
    private async Task<(NormalizedSql Normalized, string KeyInUrl)> ReadQueryAsync(
        IReadOnlyList<FieldValue> key,
        CancellationToken cancellationToken)
    {
        await using var session = await store.OpenSessionAsync(cancellationToken);

        var row = await session.FindAsync(Definition, key, Definition.Fields, cancellationToken)
            ?? throw SapErrors.EntityNotFound();

        var columns = await session.ReadTableColumnsAsync(SqlQueryPolicy.AllowedTables, cancellationToken);
        return (SqlNormalizer.Normalize(row.Text("SqlText")!, columns), EntityKey.Format(Definition, row));
    }

    private static void EnsureParametersMatch(NormalizedSql normalized, IReadOnlyList<SqlParameter> parameters)
    {
        var given = parameters.Select(parameter => parameter.Name).ToList();
        var isMatch = given.Count == given.Distinct().Count() && given.ToHashSet().SetEquals(normalized.Parameters);

        if (!isMatch)
        {
            throw SapErrors.SqlParameterError();
        }
    }

    private static JsonArray ToJson(IEnumerable<SqlRow> rows)
    {
        var array = new JsonArray();

        foreach (var row in rows)
        {
            var json = new JsonObject();
            foreach (var cell in row.Cells)
            {
                json[cell.Name] = ToJsonValue(cell.Value);
            }

            array.Add(json);
        }

        return array;
    }

    private static JsonNode? ToJsonValue(object? value) => value switch
    {
        null => null,
        string text => JsonValue.Create(text),
        short number => JsonValue.Create(number),
        int number => JsonValue.Create(number),
        long number => JsonValue.Create(number),
        decimal number => JsonValue.Create((double)number),
        double number => JsonValue.Create(number),
        bool flag => JsonValue.Create(flag),

        // List shows dates differently from objects: 20201007 (guide p.153).
        DateTime date => JsonValue.Create(date.ToString("yyyyMMdd", CultureInfo.InvariantCulture)),
        DateOnly date => JsonValue.Create(date.ToString("yyyyMMdd", CultureInfo.InvariantCulture)),
        _ => JsonValue.Create(Convert.ToString(value, CultureInfo.InvariantCulture)),
    };
}
