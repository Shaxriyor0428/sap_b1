using MockSl.Application.Entities;
using MockSl.Domain.Metadata;

namespace MockSl.Infrastructure.Persistence;

/// <summary>Builds the SQL for the rows of a collection, such as document lines in INV1.</summary>
public static class ChildSqlBuilder
{
    /// <summary>Reads every column, because the rules also need columns that are not in the API.</summary>
    public static SqlStatement Select(ChildDefinition child, IReadOnlyList<object?> parentKeys)
    {
        var parameters = new ParameterBag();
        var parent = SqlNames.Quote(child.ParentKeyColumn);
        var names = string.Join(", ", parentKeys.Select(parameters.Add));

        var sql = $"SELECT * FROM {SqlNames.Quote(child.Table)} WHERE {parent} IN ({names}) "
            + $"ORDER BY {parent} ASC, {SqlNames.Quote(child.RowKey.Column)} ASC";

        return new SqlStatement(sql, parameters.Values);
    }

    public static SqlStatement Insert(ChildDefinition child, object? parentKey, IReadOnlyList<FieldValue> values)
    {
        var parameters = new ParameterBag();
        var columns = new[] { child.ParentKeyColumn }.Concat(values.Select(value => value.Field.Column)).Select(SqlNames.Quote);
        var names = new[] { parentKey }.Concat(values.Select(value => value.Value)).Select(parameters.Add).ToList();

        var sql = $"INSERT INTO {SqlNames.Quote(child.Table)} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", names)})";
        return new SqlStatement(sql, parameters.Values);
    }

    public static SqlStatement Update(ChildDefinition child, object? parentKey, object? rowKey, IReadOnlyList<FieldValue> values)
    {
        var parameters = new ParameterBag();
        var assignments = Assignments(values, parameters);

        var sql = $"UPDATE {SqlNames.Quote(child.Table)} SET {assignments} "
            + $"WHERE {SqlNames.Quote(child.ParentKeyColumn)} = {parameters.Add(parentKey)} "
            + $"AND {SqlNames.Quote(child.RowKey.Column)} = {parameters.Add(rowKey)}";

        return new SqlStatement(sql, parameters.Values);
    }

    public static SqlStatement UpdateAll(ChildDefinition child, object? parentKey, IReadOnlyList<FieldValue> values)
    {
        var parameters = new ParameterBag();
        var assignments = Assignments(values, parameters);

        var sql = $"UPDATE {SqlNames.Quote(child.Table)} SET {assignments} "
            + $"WHERE {SqlNames.Quote(child.ParentKeyColumn)} = {parameters.Add(parentKey)}";

        return new SqlStatement(sql, parameters.Values);
    }

    private static string Assignments(IReadOnlyList<FieldValue> values, ParameterBag parameters) =>
        string.Join(", ", values.Select(value => $"{SqlNames.Quote(value.Field.Column)} = {parameters.Add(value.Value)}"));
}
