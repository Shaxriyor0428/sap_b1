using MockSl.Domain.Errors;
using MockSl.Domain.Metadata;

namespace MockSl.Application.OData;

/// <summary>
/// Recursive descent parser for $filter. Each method handles one precedence level,
/// lowest first, which makes "and" bind tighter than "or":
///
///   or         := and ("or" and)*
///   and        := unary ("and" unary)*
///   unary      := "not" unary | "(" or ")" | function | comparison
///   function   := contains(field,'x') | startswith(field,'x') | endswith(field,'x') | substringof('x',field)
///   comparison := field operator literal
/// </summary>
public sealed class FilterParser
{
    // Stops requests such as "((((((...": they would otherwise exhaust the stack.
    private const int MaxDepth = 32;

    private static readonly Dictionary<string, ComparisonOperator> Operators = new()
    {
        ["eq"] = ComparisonOperator.Equal,
        ["ne"] = ComparisonOperator.NotEqual,
        ["gt"] = ComparisonOperator.GreaterThan,
        ["ge"] = ComparisonOperator.GreaterOrEqual,
        ["lt"] = ComparisonOperator.LessThan,
        ["le"] = ComparisonOperator.LessOrEqual,
    };

    private readonly EntityDefinition _entity;
    private readonly IReadOnlyList<Token> _tokens;
    private int _position;
    private int _depth;

    private FilterParser(EntityDefinition entity, IReadOnlyList<Token> tokens)
    {
        _entity = entity;
        _tokens = tokens;
    }

    private Token Current => _tokens[_position];

    public static FilterNode Parse(EntityDefinition entity, string filter)
    {
        try
        {
            var parser = new FilterParser(entity, ODataLexer.Tokenize(filter));
            var node = parser.ParseOr();
            parser.Expect(TokenKind.End, "end of expression");
            return node;
        }
        catch (ODataSyntaxException exception)
        {
            throw SapErrors.InvalidFilter(exception.Message);
        }
    }

    private FilterNode ParseOr()
    {
        var left = ParseAnd();

        while (TryConsumeKeyword("or"))
        {
            left = new OrNode(left, ParseAnd());
        }

        return left;
    }

    private FilterNode ParseAnd()
    {
        var left = ParseUnary();

        while (TryConsumeKeyword("and"))
        {
            left = new AndNode(left, ParseUnary());
        }

        return left;
    }

    private FilterNode ParseUnary()
    {
        if (++_depth > MaxDepth)
        {
            throw new ODataSyntaxException("expression is nested too deeply");
        }

        try
        {
            if (TryConsumeKeyword("not"))
            {
                return new NotNode(ParseUnary());
            }

            if (TryConsume(TokenKind.OpenParen))
            {
                var inner = ParseOr();
                Expect(TokenKind.CloseParen, "')'");
                return inner;
            }

            var name = Expect(TokenKind.Identifier, "a field or function name");
            return Current.Kind == TokenKind.OpenParen ? ParseFunction(name) : ParseComparison(name);
        }
        finally
        {
            _depth--;
        }
    }

    private FilterNode ParseFunction(Token name)
    {
        Expect(TokenKind.OpenParen, "'('");

        FilterNode node;
        if (name.Text == "substringof")
        {
            var text = Expect(TokenKind.String, "a text value");
            Expect(TokenKind.Comma, "','");
            node = new TextMatchNode(ReadTextField(), TextMatchKind.Contains, text.Text);
        }
        else
        {
            var kind = name.Text switch
            {
                "contains" => TextMatchKind.Contains,
                "startswith" => TextMatchKind.StartsWith,
                "endswith" => TextMatchKind.EndsWith,
                _ => throw new ODataSyntaxException($"unknown function '{name.Text}'"),
            };
            var field = ReadTextField();
            Expect(TokenKind.Comma, "','");
            node = new TextMatchNode(field, kind, Expect(TokenKind.String, "a text value").Text);
        }

        Expect(TokenKind.CloseParen, "')'");
        return node;
    }

    private FilterNode ParseComparison(Token name)
    {
        var field = ResolveField(name);
        var operatorToken = Expect(TokenKind.Identifier, "a comparison operator");

        if (!Operators.TryGetValue(operatorToken.Text, out var comparison))
        {
            throw new ODataSyntaxException($"unknown operator '{operatorToken.Text}'");
        }

        if (Current is { Kind: TokenKind.Identifier, Text: "null" })
        {
            _position++;
            return comparison is ComparisonOperator.Equal or ComparisonOperator.NotEqual
                ? new ComparisonNode(field, comparison, null)
                : throw new ODataSyntaxException("null can only be used with eq and ne");
        }

        if (Current.Kind is not (TokenKind.String or TokenKind.Number))
        {
            throw new ODataSyntaxException($"expected a value at position {Current.Position}");
        }

        var literal = Current;
        _position++;
        return new ComparisonNode(field, comparison, LiteralConverter.Convert(field, literal));
    }

    private FieldDefinition ReadTextField()
    {
        var field = ResolveField(Expect(TokenKind.Identifier, "a field name"));
        return field.Type == FieldType.Text
            ? field
            : throw new ODataSyntaxException($"'{field.Name}' is not a text field");
    }

    private FieldDefinition ResolveField(Token name) =>
        _entity.FindField(name.Text) ?? throw SapErrors.InvalidProperty(_entity.TypeName, name.Text);

    private bool TryConsumeKeyword(string keyword)
    {
        if (Current.Kind == TokenKind.Identifier && Current.Text == keyword)
        {
            _position++;
            return true;
        }

        return false;
    }

    private bool TryConsume(TokenKind kind)
    {
        if (Current.Kind == kind)
        {
            _position++;
            return true;
        }

        return false;
    }

    private Token Expect(TokenKind kind, string description)
    {
        if (Current.Kind != kind)
        {
            throw new ODataSyntaxException($"expected {description} at position {Current.Position}");
        }

        var token = Current;
        _position++;
        return token;
    }
}
