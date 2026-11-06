using MockSl.Application.Entities;
using MockSl.Domain.Metadata;

namespace MockSl.Infrastructure.Persistence;

/// <summary>Builds the SQL for every generic operation from an object definition.</summary>
public static class EntitySqlBuilder
{
    public static SqlStatement Select(EntityDefinition entity, EntityQuery query)
    {
        var parameters = new ParameterBag();
        var sql = $"SELECT {ColumnList(query.Columns)} FROM {SqlNames.Quote(entity.Table)}";

        if (query.Filter is not null)
        {
            sql += $" WHERE {FilterSqlBuilder.Build(query.Filter, parameters)}";
        }

        sql += $" ORDER BY {OrderBy(entity, query)}";

        if (query.Limit is { } limit)
        {
            sql += $" LIMIT {parameters.Add(limit)}";
        }

        if (query.Offset > 0)
        {
            sql += $" OFFSET {parameters.Add(query.Offset)}";
        }

        return new SqlStatement(sql, parameters.Values);
    }

    public static SqlStatement FindByKey(
        EntityDefinition entity,
        IReadOnlyList<FieldValue> key,
        IReadOnlyList<FieldDefinition> columns)
    {
        var parameters = new ParameterBag();
        var sql = $"SELECT {ColumnList(columns)} FROM {SqlNames.Quote(entity.Table)} WHERE {KeyCondition(key, parameters)}";
        return new SqlStatement(sql, parameters.Values);
    }

    public static SqlStatement Insert(EntityDefinition entity, IReadOnlyList<FieldValue> values)
    {
        var parameters = new ParameterBag();
        var table = SqlNames.Quote(entity.Table);

        if (values.Count == 0)
        {
            return new SqlStatement($"INSERT INTO {table} DEFAULT VALUES RETURNING *", parameters.Values);
        }

        var columns = ColumnList(values.Select(value => value.Field));
        var names = string.Join(", ", values.Select(value => parameters.Add(value.Value)));
        return new SqlStatement($"INSERT INTO {table} ({columns}) VALUES ({names}) RETURNING *", parameters.Values);
    }

    public static SqlStatement Update(
        EntityDefinition entity,
        IReadOnlyList<FieldValue> key,
        IReadOnlyList<FieldValue> values)
    {
        var parameters = new ParameterBag();
        var assignments = string.Join(
            ", ",
            values.Select(value => $"{SqlNames.Quote(value.Field.Column)} = {parameters.Add(value.Value)}"));

        var sql = $"UPDATE {SqlNames.Quote(entity.Table)} SET {assignments} WHERE {KeyCondition(key, parameters)}";
        return new SqlStatement(sql, parameters.Values);
    }

    public static SqlStatement Delete(EntityDefinition entity, IReadOnlyList<FieldValue> key)
    {
        var parameters = new ParameterBag();
        var sql = $"DELETE FROM {SqlNames.Quote(entity.Table)} WHERE {KeyCondition(key, parameters)}";
        return new SqlStatement(sql, parameters.Values);
    }

    private static string ColumnList(IEnumerable<FieldDefinition> fields) =>
        string.Join(", ", fields.Select(field => SqlNames.Quote(field.Column)));

    private static string KeyCondition(IEnumerable<FieldValue> key, ParameterBag parameters) =>
        string.Join(" AND ", key.Select(part => $"{SqlNames.Quote(part.Field.Column)} = {parameters.Add(part.Value)}"));

    /// <summary>
    /// The key always closes the sort order. Without a unique tie-breaker, rows with
    /// equal values could move between pages, so paging could repeat or skip them.
    /// </summary>
    private static string OrderBy(EntityDefinition entity, EntityQuery query)
    {
        var requested = query.OrderBy.Select(item =>
            $"{SqlNames.Quote(item.Field.Column)} {(item.Descending ? "DESC" : "ASC")}");

        var orderedFields = query.OrderBy.Select(item => item.Field).ToHashSet();
        var tieBreaker = entity.Key
            .Where(field => !orderedFields.Contains(field))
            .Select(field => $"{SqlNames.Quote(field.Column)} ASC");

        return string.Join(", ", requested.Concat(tieBreaker));
    }
}
