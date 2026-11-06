using MockSl.Domain.Errors;
using MockSl.Domain.Metadata;

namespace MockSl.Application.OData;

/// <summary>What the client wrote in $expand. Select is null when it gave no $select inside the brackets.</summary>
public sealed record ExpandRequest(NavigationDefinition Navigation, IReadOnlyList<FieldDefinition>? Select);

/// <summary>
/// Reads the $expand option (guide p.50):
///   expand := item ("," item)*
///   item   := navigation [ "(" "$select" "=" field ("," field)* ")" ]
/// </summary>
public sealed class ExpandParser
{
    private readonly EntityDefinition _entity;
    private readonly IReadOnlyList<Token> _tokens;
    private int _position;

    private ExpandParser(EntityDefinition entity, IReadOnlyList<Token> tokens)
    {
        _entity = entity;
        _tokens = tokens;
    }

    private Token Current => _tokens[_position];

    public static IReadOnlyList<ExpandRequest> Parse(EntityDefinition entity, string expand)
    {
        try
        {
            return new ExpandParser(entity, ODataLexer.Tokenize(expand)).ParseItems();
        }
        catch (ODataSyntaxException)
        {
            throw SapErrors.InvalidQueryOption("$expand", expand);
        }
    }

    private List<ExpandRequest> ParseItems()
    {
        var items = new List<ExpandRequest> { ParseItem() };

        while (TryConsume(TokenKind.Comma))
        {
            items.Add(ParseItem());
        }

        Expect(TokenKind.End);
        return items;
    }

    private ExpandRequest ParseItem()
    {
        var name = Expect(TokenKind.Identifier).Text;
        var navigation = _entity.FindNavigation(name)
            ?? throw SapErrors.InvalidProperty(_entity.TypeName, name);

        return TryConsume(TokenKind.OpenParen)
            ? new ExpandRequest(navigation, ParseSelect(navigation))
            : new ExpandRequest(navigation, null);
    }

    private List<FieldDefinition> ParseSelect(NavigationDefinition navigation)
    {
        var option = Expect(TokenKind.Identifier).Text;
        if (option != "$select")
        {
            throw SapErrors.UnsupportedQueryOption(option);
        }

        Expect(TokenKind.Equal);

        var fields = new List<FieldDefinition> { ParseField(navigation) };
        while (TryConsume(TokenKind.Comma))
        {
            fields.Add(ParseField(navigation));
        }

        Expect(TokenKind.CloseParen);
        return fields;
    }

    private FieldDefinition ParseField(NavigationDefinition navigation)
    {
        var name = Expect(TokenKind.Identifier).Text;
        var target = navigation.Target;
        return target.FindField(name) ?? throw SapErrors.InvalidProperty(target.TypeName, name);
    }

    private bool TryConsume(TokenKind kind)
    {
        if (Current.Kind != kind)
        {
            return false;
        }

        _position++;
        return true;
    }

    private Token Expect(TokenKind kind)
    {
        if (Current.Kind != kind)
        {
            throw new ODataSyntaxException($"expected {kind} at position {Current.Position}");
        }

        var token = Current;
        _position++;
        return token;
    }
}
