namespace MockSl.Application.OData;

public enum TokenKind
{
    Identifier,
    String,
    Number,
    OpenParen,
    CloseParen,
    Comma,
    Equal,
    Slash,
    End,
}

public readonly record struct Token(TokenKind Kind, string Text, int Position);

public sealed class ODataSyntaxException(string message) : Exception(message);

/// <summary>
/// Splits OData text into tokens. Used for both $filter and URL keys,
/// because both share the same literal rules: 'text', 42 and escaped quotes ('').
/// </summary>
public static class ODataLexer
{
    public static IReadOnlyList<Token> Tokenize(string text)
    {
        var tokens = new List<Token>();
        var index = 0;

        while (index < text.Length)
        {
            var current = text[index];

            if (char.IsWhiteSpace(current))
            {
                index++;
            }
            else if (SymbolKind(current) is { } symbol)
            {
                tokens.Add(new Token(symbol, current.ToString(), index));
                index++;
            }
            else if (current == '\'')
            {
                tokens.Add(ReadString(text, ref index));
            }
            else if (StartsNumber(text, index))
            {
                tokens.Add(ReadNumber(text, ref index));
            }
            else if (StartsIdentifier(current))
            {
                tokens.Add(ReadIdentifierOrDateTime(text, ref index));
            }
            else
            {
                throw new ODataSyntaxException($"unexpected character '{current}' at position {index}");
            }
        }

        tokens.Add(new Token(TokenKind.End, string.Empty, index));
        return tokens;
    }

    private static TokenKind? SymbolKind(char value) => value switch
    {
        '(' => TokenKind.OpenParen,
        ')' => TokenKind.CloseParen,
        ',' => TokenKind.Comma,
        '=' => TokenKind.Equal,
        '/' => TokenKind.Slash,
        _ => null,
    };

    private static Token ReadString(string text, ref int index)
    {
        var start = index;
        var value = new System.Text.StringBuilder();
        index++;

        while (index < text.Length)
        {
            if (text[index] != '\'')
            {
                value.Append(text[index]);
                index++;
            }
            else if (index + 1 < text.Length && text[index + 1] == '\'')
            {
                value.Append('\'');
                index += 2;
            }
            else
            {
                index++;
                return new Token(TokenKind.String, value.ToString(), start);
            }
        }

        throw new ODataSyntaxException($"unterminated string starting at position {start}");
    }

    private static bool StartsNumber(string text, int index) =>
        char.IsAsciiDigit(text[index])
        || (text[index] == '-' && index + 1 < text.Length && char.IsAsciiDigit(text[index + 1]));

    private static Token ReadNumber(string text, ref int index)
    {
        var start = index;
        index++;

        while (index < text.Length && (char.IsAsciiDigit(text[index]) || text[index] == '.'))
        {
            index++;
        }

        return new Token(TokenKind.Number, text[start..index], start);
    }

    private static bool StartsIdentifier(char value) =>
        char.IsAsciiLetter(value) || value == '_' || value == '$';

    private static Token ReadIdentifierOrDateTime(string text, ref int index)
    {
        var start = index;

        while (index < text.Length && (char.IsAsciiLetterOrDigit(text[index]) || text[index] == '_' || text[index] == '$'))
        {
            index++;
        }

        var name = text[start..index];

        // datetime'2014-04-23' (guide p.34) is a date written with an optional prefix.
        var isDateTimeLiteral = name == "datetime" && index < text.Length && text[index] == '\'';
        return isDateTimeLiteral
            ? ReadString(text, ref index)
            : new Token(TokenKind.Identifier, name, start);
    }
}
