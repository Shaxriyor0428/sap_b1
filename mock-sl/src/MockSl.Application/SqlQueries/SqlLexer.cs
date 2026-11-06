using MockSl.Domain.Errors;

namespace MockSl.Application.SqlQueries;

public enum SqlTokenKind
{
    /// <summary>A keyword, or a name written without quotes.</summary>
    Word,

    /// <summary>A name in "double quotes" or [brackets]. Text holds it without them.</summary>
    QuotedName,
    String,
    Number,
    Parameter,
    Symbol,
    End,
}

/// <param name="Start">Where the token begins in the SQL text.</param>
/// <param name="End">Where the text after the token begins.</param>
public readonly record struct SqlToken(SqlTokenKind Kind, string Text, int Start, int End);

/// <summary>
/// Splits saved SQL into tokens. Comments and more than one statement stop here: both are
/// common ways to smuggle something past a check, and no task needs them.
/// </summary>
public static class SqlLexer
{
    private static readonly string[] TwoCharacterSymbols = ["<>", "!=", ">=", "<="];

    private const string OneCharacterSymbols = "=<>(),.*+-/";

    public static IReadOnlyList<SqlToken> Tokenize(string sql)
    {
        var tokens = new List<SqlToken>();
        var index = 0;

        while (index < sql.Length)
        {
            var current = sql[index];
            var next = index + 1 < sql.Length ? sql[index + 1] : '\0';

            if (char.IsWhiteSpace(current))
            {
                index++;
                continue;
            }

            if ((current == '-' && next == '-') || (current == '/' && next == '*'))
            {
                throw SapErrors.SqlSyntax(sql, index, "Comments are not supported.");
            }

            if (current == ';')
            {
                throw SapErrors.SqlSyntax(sql, index, "More than one statement is not supported.");
            }

            var token = current switch
            {
                '\'' => ReadString(sql, index),
                '"' => ReadQuotedName(sql, index, '"'),
                '[' => ReadQuotedName(sql, index, ']'),
                ':' => ReadParameter(sql, index),
                _ when char.IsAsciiDigit(current) => ReadNumber(sql, index),
                _ when char.IsAsciiLetter(current) || current == '_' => ReadWord(sql, index),
                _ => ReadSymbol(sql, index),
            };

            tokens.Add(token);
            index = token.End;
        }

        tokens.Add(new SqlToken(SqlTokenKind.End, string.Empty, sql.Length, sql.Length));
        return tokens;
    }

    // The quotes stay in Text: the literal is copied into the query exactly as it was written.
    private static SqlToken ReadString(string sql, int start)
    {
        var index = start + 1;

        while (index < sql.Length)
        {
            if (sql[index] != '\'')
            {
                index++;
            }
            else if (index + 1 < sql.Length && sql[index + 1] == '\'')
            {
                index += 2;
            }
            else
            {
                return new SqlToken(SqlTokenKind.String, sql[start..(index + 1)], start, index + 1);
            }
        }

        throw SapErrors.SqlSyntax(sql, start, "The string is not closed.");
    }

    private static SqlToken ReadQuotedName(string sql, int start, char closing)
    {
        var end = sql.IndexOf(closing, start + 1);
        if (end < 0)
        {
            throw SapErrors.SqlSyntax(sql, start, "The name is not closed.");
        }

        return new SqlToken(SqlTokenKind.QuotedName, sql[(start + 1)..end], start, end + 1);
    }

    private static SqlToken ReadParameter(string sql, int start)
    {
        var end = start + 1;
        while (end < sql.Length && IsWordCharacter(sql[end]))
        {
            end++;
        }

        // Also stops the PostgreSQL cast "::", which starts with a colon and no name.
        if (end == start + 1 || char.IsAsciiDigit(sql[start + 1]))
        {
            throw SapErrors.SqlSyntax(sql, start, "A parameter name is expected after ':'.");
        }

        return new SqlToken(SqlTokenKind.Parameter, sql[(start + 1)..end], start, end);
    }

    private static SqlToken ReadNumber(string sql, int start)
    {
        var end = start;
        while (end < sql.Length && (char.IsAsciiDigit(sql[end]) || sql[end] == '.'))
        {
            end++;
        }

        return new SqlToken(SqlTokenKind.Number, sql[start..end], start, end);
    }

    private static SqlToken ReadWord(string sql, int start)
    {
        var end = start;
        while (end < sql.Length && IsWordCharacter(sql[end]))
        {
            end++;
        }

        return new SqlToken(SqlTokenKind.Word, sql[start..end], start, end);
    }

    private static SqlToken ReadSymbol(string sql, int start)
    {
        foreach (var symbol in TwoCharacterSymbols)
        {
            if (string.CompareOrdinal(sql, start, symbol, 0, symbol.Length) == 0)
            {
                return new SqlToken(SqlTokenKind.Symbol, symbol, start, start + symbol.Length);
            }
        }

        if (OneCharacterSymbols.Contains(sql[start]))
        {
            return new SqlToken(SqlTokenKind.Symbol, sql[start].ToString(), start, start + 1);
        }

        throw SapErrors.SqlSyntax(sql, start, $"Incorrect syntax near '{sql[start]}'.");
    }

    private static bool IsWordCharacter(char character) => char.IsAsciiLetterOrDigit(character) || character == '_';
}
