using MockSl.Domain.Errors;

namespace MockSl.Application.OData;

/// <summary>One part of a URL key. Name is null in the short form: ('C0001').</summary>
public sealed record KeyPart(string? Name, Token Value);

/// <summary>
/// A parsed URL path, for example:
///   BusinessPartners                    -> no key
///   BusinessPartners('C0001')           -> one unnamed key part
///   UserFieldsMD(TableName='OCRD',FieldID=0) -> two named key parts
///   Invoices(1)/Cancel                  -> key and action
/// </summary>
public sealed record ResourcePath(string EntitySet, IReadOnlyList<KeyPart>? Key, string? Action)
{
    public static ResourcePath Parse(string path)
    {
        try
        {
            return new Parser(ODataLexer.Tokenize(path)).ParsePath();
        }
        catch (ODataSyntaxException)
        {
            throw SapErrors.UnrecognizedResource();
        }
    }

    private sealed class Parser(IReadOnlyList<Token> tokens)
    {
        private int _position;

        private Token Current => tokens[_position];

        public ResourcePath ParsePath()
        {
            var entitySet = Expect(TokenKind.Identifier).Text;
            var key = Current.Kind == TokenKind.OpenParen ? ParseKey() : null;

            string? action = null;
            if (Current.Kind == TokenKind.Slash)
            {
                _position++;
                action = Expect(TokenKind.Identifier).Text;
            }

            Expect(TokenKind.End);
            return new ResourcePath(entitySet, key, action);
        }

        private List<KeyPart> ParseKey()
        {
            Expect(TokenKind.OpenParen);
            var parts = new List<KeyPart> { ParseKeyPart() };

            while (Current.Kind == TokenKind.Comma)
            {
                _position++;
                parts.Add(ParseKeyPart());
            }

            Expect(TokenKind.CloseParen);
            return parts;
        }

        private KeyPart ParseKeyPart()
        {
            string? name = null;
            if (Current.Kind == TokenKind.Identifier)
            {
                name = Current.Text;
                _position++;
                Expect(TokenKind.Equal);
            }

            if (Current.Kind is not (TokenKind.String or TokenKind.Number))
            {
                throw new ODataSyntaxException("key value expected");
            }

            var value = Current;
            _position++;
            return new KeyPart(name, value);
        }

        private Token Expect(TokenKind kind)
        {
            if (Current.Kind != kind)
            {
                throw new ODataSyntaxException($"expected {kind}");
            }

            var token = Current;
            _position++;
            return token;
        }
    }
}
