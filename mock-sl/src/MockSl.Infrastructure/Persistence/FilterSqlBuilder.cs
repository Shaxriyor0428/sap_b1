using MockSl.Application.OData;

namespace MockSl.Infrastructure.Persistence;

/// <summary>
/// Turns a parsed $filter tree into a SQL condition.
/// Columns come from definitions and values become parameters, so user text cannot reach the SQL.
/// </summary>
public static class FilterSqlBuilder
{
    public static string Build(FilterNode node, ParameterBag parameters) => node switch
    {
        AndNode and => $"({Build(and.Left, parameters)} AND {Build(and.Right, parameters)})",
        OrNode or => $"({Build(or.Left, parameters)} OR {Build(or.Right, parameters)})",
        NotNode not => $"NOT ({Build(not.Inner, parameters)})",
        ComparisonNode comparison => BuildComparison(comparison, parameters),
        TextMatchNode match => BuildTextMatch(match, parameters),
        _ => throw new InvalidOperationException($"Unhandled filter node {node.GetType().Name}"),
    };

    private static string BuildComparison(ComparisonNode node, ParameterBag parameters)
    {
        var column = SqlNames.Quote(node.Field.Column);

        if (node.Value is null)
        {
            return node.Operator == ComparisonOperator.Equal ? $"{column} IS NULL" : $"{column} IS NOT NULL";
        }

        return $"{column} {ToSql(node.Operator)} {parameters.Add(node.Value)}";
    }

    private static string BuildTextMatch(TextMatchNode node, ParameterBag parameters)
    {
        var text = EscapeLikePattern(node.Text);
        var pattern = node.Kind switch
        {
            TextMatchKind.Contains => $"%{text}%",
            TextMatchKind.StartsWith => $"{text}%",
            TextMatchKind.EndsWith => $"%{text}",
            _ => throw new InvalidOperationException($"Unhandled match kind {node.Kind}"),
        };

        return $"{SqlNames.Quote(node.Field.Column)} LIKE {parameters.Add(pattern)} ESCAPE '\\'";
    }

    // Without this, a user searching for "50%" would match everything starting with "50".
    private static string EscapeLikePattern(string text) =>
        text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static string ToSql(ComparisonOperator comparison) => comparison switch
    {
        ComparisonOperator.Equal => "=",
        ComparisonOperator.NotEqual => "<>",
        ComparisonOperator.GreaterThan => ">",
        ComparisonOperator.GreaterOrEqual => ">=",
        ComparisonOperator.LessThan => "<",
        ComparisonOperator.LessOrEqual => "<=",
        _ => throw new InvalidOperationException($"Unhandled operator {comparison}"),
    };
}
