using MockSl.Domain.Metadata;

namespace MockSl.Application.OData;

public enum ComparisonOperator
{
    Equal,
    NotEqual,
    GreaterThan,
    GreaterOrEqual,
    LessThan,
    LessOrEqual,
}

public enum TextMatchKind
{
    Contains,
    StartsWith,
    EndsWith,
}

/// <summary>
/// A parsed $filter as a tree. It holds resolved fields and converted values,
/// never raw user text, so the SQL layer can only emit known columns and parameters.
/// </summary>
public abstract record FilterNode;

public sealed record AndNode(FilterNode Left, FilterNode Right) : FilterNode;

public sealed record OrNode(FilterNode Left, FilterNode Right) : FilterNode;

public sealed record NotNode(FilterNode Inner) : FilterNode;

public sealed record ComparisonNode(FieldDefinition Field, ComparisonOperator Operator, object? Value) : FilterNode;

public sealed record TextMatchNode(FieldDefinition Field, TextMatchKind Kind, string Text) : FilterNode;
