using System.Text;
using MockSl.Domain.Errors;
using MockSl.Domain.SqlQueries;

namespace MockSl.Application.SqlQueries;

/// <summary>One saved query in the two forms it is needed.</summary>
/// <param name="SqlText">What is stored and shown to the client, in the form the guide shows for SAP HANA.</param>
/// <param name="Executable">What runs on PostgreSQL: @name for parameters, LIMIT for TOP, COALESCE for IFNULL.</param>
/// <param name="Parameters">The parameter names, each once.</param>
public sealed record NormalizedSql(string SqlText, string Executable, IReadOnlyList<string> Parameters)
{
    public string? ParamList => Parameters.Count == 0 ? null : string.Join(',', Parameters);
}

/// <summary>
/// Checks saved SQL and puts every name into its exact spelling (guide p.148-157).
/// This is a simplified copy of what the real Service Layer does, not a full SQL parser.
///
/// The safety does not depend on understanding all of SQL. Every name reaching the database is either
/// taken from the catalog or has passed IsPlainName, functions and keywords come from fixed lists, values
/// are parameters. What this class does not notice, such as a wrong GROUP BY, is refused by PostgreSQL
/// inside a read-only transaction.
/// </summary>
public sealed class SqlNormalizer
{
    private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "FROM", "WHERE", "AND", "OR", "NOT", "AS", "ON", "JOIN", "INNER", "LEFT", "RIGHT", "FULL", "OUTER",
        "GROUP", "ORDER", "BY", "HAVING", "ASC", "DESC", "IS", "NULL", "LIKE", "BETWEEN", "IN", "EXISTS", "UNION",
        "ALL", "DISTINCT", "TOP",
    };

    /// <summary>Guide p.149.</summary>
    private static readonly HashSet<string> Functions = new(StringComparer.OrdinalIgnoreCase)
    {
        "SUM", "AVG", "MAX", "MIN", "COUNT", "IFNULL", "ISNULL", "LOWER", "UPPER", "LEFT", "RIGHT",
    };

    /// <summary>Words that are valid SQL elsewhere but outside the guide's list (p.148-149).</summary>
    private static readonly HashSet<string> NotSupported = new(StringComparer.OrdinalIgnoreCase)
    {
        "LIMIT", "OFFSET", "FETCH", "INTO", "WITH", "FOR", "CASE", "WHEN", "CAST", "CROSS", "INSERT", "UPDATE",
        "DELETE", "DROP", "ALTER", "CREATE", "TRUNCATE", "GRANT", "REVOKE", "EXCEPT", "INTERSECT",
    };

    private readonly string _sql;
    private readonly IReadOnlyList<SqlToken> _tokens;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _columnsByTable;

    // What each token is, found before the rewrite because SELECT comes before the FROM that explains it.
    private readonly Dictionary<int, string> _tableTokens = [];
    private readonly HashSet<int> _tableAliasTokens = [];
    private readonly HashSet<int> _columnAliasTokens = [];

    // One alias can name several tables: the guide's own samples use t1 on both sides of UNION (p.148).
    private readonly Dictionary<string, List<string>> _tablesByQualifier = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _tables = [];
    private readonly HashSet<string> _tableAliases = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _columnAliases = [];
    private readonly List<string> _parameters = [];

    private readonly StringBuilder _text = new();
    private readonly StringBuilder _executable = new();

    private SqlNormalizer(string sql, IReadOnlyDictionary<string, IReadOnlyList<string>> columnsByTable)
    {
        _sql = sql;
        _tokens = SqlLexer.Tokenize(sql);
        _columnsByTable = columnsByTable;
    }

    /// <param name="sql">The text from the client, or the stored text: it can be normalized again.</param>
    /// <param name="columnsByTable">The real columns of the allowed tables.</param>
    public static NormalizedSql Normalize(string sql, IReadOnlyDictionary<string, IReadOnlyList<string>> columnsByTable)
    {
        var normalizer = new SqlNormalizer(sql, columnsByTable);

        normalizer.EnsureStartsWithSelect();
        normalizer.FindTables();
        normalizer.FindSelectLists();
        normalizer.Rewrite();

        return new NormalizedSql(normalizer._text.ToString(), normalizer._executable.ToString(), normalizer._parameters);
    }

    private void EnsureStartsWithSelect()
    {
        if (!IsKeyword(0, "SELECT"))
        {
            throw Error(0, $"mismatched input '{_tokens[0].Text}' expecting {{SELECT, '('}}");
        }
    }

    // Tables, including those in sub queries and after UNION.
    private void FindTables()
    {
        for (var index = 0; index < _tokens.Count; index++)
        {
            if (IsKeyword(index, "FROM") || IsKeyword(index, "JOIN"))
            {
                index = ReadTableList(index + 1) - 1;
            }
        }

        if (_tables.Count == 0)
        {
            throw Error(_sql.Length, "FROM is expected.");
        }
    }

    private int ReadTableList(int index)
    {
        index = ReadTable(index);

        while (IsSymbol(index, ","))
        {
            index = ReadTable(index + 1);
        }

        return index;
    }

    // Returns where the text continues after the table and its alias.
    private int ReadTable(int index)
    {
        var token = At(index);

        if (IsSymbol(index, "("))
        {
            throw Error(token.Start, "A sub query in FROM is not supported.");
        }

        if (!IsName(index) || IsReserved(index))
        {
            throw Error(token.Start, "A table name is expected.");
        }

        // Upper case because SAP table names are, and the guide accepts "oitm" (p.150).
        var table = token.Text.ToUpperInvariant();

        // A dot means schema.table, the way out of the allowed list.
        if (IsSymbol(index + 1, ".") || !SqlQueryPolicy.AllowedTables.Contains(table) || !_columnsByTable.ContainsKey(table))
        {
            throw SapErrors.SqlTableNotAccessible(token.Text);
        }

        _tableTokens[index] = table;
        _tables.Add(table);
        AddQualifier(table, table);

        var next = IsKeyword(index + 1, "AS") ? index + 2 : index + 1;
        if (!IsName(next) || IsReserved(next))
        {
            return next;
        }

        EnsurePlainName(At(next));
        _tableAliasTokens.Add(next);
        _tableAliases.Add(At(next).Text);
        AddQualifier(At(next).Text, table);
        return next + 1;
    }

    private void AddQualifier(string qualifier, string table)
    {
        if (!_tablesByQualifier.TryGetValue(qualifier, out var tables))
        {
            tables = [];
            _tablesByQualifier[qualifier] = tables;
        }

        tables.Add(table);
    }

    private void FindSelectLists()
    {
        var depth = 0;

        for (var index = 0; index < _tokens.Count; index++)
        {
            if (IsSymbol(index, "("))
            {
                depth++;
            }
            else if (IsSymbol(index, ")"))
            {
                depth--;
            }
            else if (IsKeyword(index, "SELECT"))
            {
                ReadSelectList(index + 1, isMainQuery: depth == 0);
            }
        }
    }

    private void ReadSelectList(int index, bool isMainQuery)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (first, last) in SplitSelectItems(index))
        {
            var name = ReadSelectItem(first, last, isMainQuery);

            // JSON cannot hold two fields with one name (guide p.155).
            if (isMainQuery && name is { } token && !names.Add(token.Text))
            {
                throw Error(token.Start, $"Multiple name for column or column alias '{token.Text}', please specify it with a different alias.");
            }
        }
    }

    private List<(int First, int Last)> SplitSelectItems(int index)
    {
        if (IsKeyword(index, "DISTINCT"))
        {
            index++;
        }

        if (IsKeyword(index, "TOP"))
        {
            index += 2;
        }

        var items = new List<(int First, int Last)>();
        var first = index;
        var depth = 0;

        for (; ; index++)
        {
            var isEnd = At(index).Kind == SqlTokenKind.End
                || (depth == 0 && (IsKeyword(index, "FROM") || IsSymbol(index, ")")));

            if (isEnd || (depth == 0 && IsSymbol(index, ",")))
            {
                items.Add((first, index - 1));
                if (isEnd)
                {
                    return items;
                }

                first = index + 1;
            }
            else if (IsSymbol(index, "("))
            {
                depth++;
            }
            else if (IsSymbol(index, ")"))
            {
                depth--;
            }
        }
    }

    // Returns the token that names the column in the answer. A sub query has no answer of its own.
    private SqlToken? ReadSelectItem(int first, int last, bool isMainQuery)
    {
        if (first > last)
        {
            throw Error(At(first).Start, $"Incorrect syntax near '{At(first).Text}'.");
        }

        var isAsterisk = (first == last && IsSymbol(first, "*"))
            || (last - first == 2 && IsSymbol(first + 1, ".") && IsSymbol(last, "*"));

        // Only the main query: select * is allowed inside a sub query (guide p.157).
        if (isMainQuery && isAsterisk)
        {
            throw Error(At(first).Start, "Cannot support asterisk(*) in select list.");
        }

        var hasAlias = last > first && IsName(last) && !IsReserved(last)
            && (IsKeyword(last - 1, "AS") || IsValueEnd(last - 1));

        if (hasAlias)
        {
            EnsurePlainName(At(last));
            _columnAliasTokens.Add(last);
            _columnAliases.Add(At(last).Text);
            return At(last);
        }

        if (first == last && IsName(first))
        {
            return At(first);
        }

        if (last - first == 2 && IsSymbol(first + 1, ".") && IsName(last))
        {
            return At(last);
        }

        // The answer needs a field name, and only an alias can give one (guide p.156).
        return isMainQuery && !isAsterisk ? throw Error(At(first).Start, "A computed column needs an alias.") : null;
    }

    private void Rewrite()
    {
        var copiedUpTo = 0;
        var depth = 0;
        string? limit = null;

        for (var index = 0; At(index).Kind != SqlTokenKind.End; index++)
        {
            var token = At(index);
            WriteToBoth(_sql[copiedUpTo..token.Start]);
            copiedUpTo = token.End;

            if (token.Kind == SqlTokenKind.Parameter)
            {
                WriteParameter(token);
            }
            else if (IsKeyword(index, "TOP"))
            {
                limit = ReadTop(index, depth);
                _text.Append(_sql[token.Start..At(index + 1).End]);
                copiedUpTo = At(index + 1).End;
                index++;
            }
            else if (token.Kind is SqlTokenKind.Word or SqlTokenKind.QuotedName)
            {
                RewriteName(index);
            }
            else
            {
                depth += IsSymbol(index, "(") ? 1 : IsSymbol(index, ")") ? -1 : 0;
                WriteToBoth(token.Text);
            }
        }

        WriteToBoth(_sql[copiedUpTo..]);

        if (depth != 0)
        {
            throw Error(_sql.Length, "The brackets do not match.");
        }

        if (limit is not null)
        {
            _executable.Append(" LIMIT ").Append(limit);
        }
    }

    // TOP has no PostgreSQL form, and a LIMIT at the end only means the same thing for the main query alone.
    private string ReadTop(int index, int depth)
    {
        var number = At(index + 1);
        var hasUnion = _tokens.Select((_, position) => position).Any(position => IsKeyword(position, "UNION"));

        if (number.Kind != SqlTokenKind.Number || number.Text.Contains('.') || depth > 0 || hasUnion)
        {
            throw Error(At(index).Start, "TOP is supported only with a whole number, in a main query without UNION.");
        }

        return number.Text;
    }

    private void WriteParameter(SqlToken token)
    {
        _text.Append(':').Append(token.Text);
        _executable.Append('@').Append(token.Text);

        if (!_parameters.Contains(token.Text))
        {
            _parameters.Add(token.Text);
        }
    }

    private void RewriteName(int index)
    {
        var token = At(index);

        if (_tableTokens.TryGetValue(index, out var table))
        {
            WriteToBoth(Quote(table));
        }
        else if (_tableAliasTokens.Contains(index))
        {
            WriteToBoth(token.Text);
        }
        else if (_columnAliasTokens.Contains(index))
        {
            WriteToBoth(Quote(token.Text));
        }
        else if (token.Kind == SqlTokenKind.Word && IsFunctionCall(index))
        {
            WriteFunction(token);
        }
        else if (token.Kind == SqlTokenKind.Word && NotSupported.Contains(token.Text))
        {
            throw Error(token.Start, $"Cannot support this function or expression: '{token.Text}'");
        }
        else if (token.Kind == SqlTokenKind.Word && Keywords.Contains(token.Text))
        {
            WriteToBoth(token.Text);
        }
        else if (IsSymbol(index + 1, "."))
        {
            WriteToBoth(Qualifier(token));
        }
        else if (IsSymbol(index - 1, "."))
        {
            WriteToBoth(Quote(QualifiedColumn(token, At(index - 2))));
        }
        else if (IsValueEnd(index - 1) && !IsKeyword(index - 2, "TOP"))
        {
            // Two names in a row are valid only as an alias, and those are already found: "order by DocEntry dsc".
            throw Error(token.Start, $"Incorrect syntax near '{token.Text}'.");
        }
        else
        {
            WriteToBoth(Quote(UnqualifiedName(token)));
        }
    }

    // LEFT and RIGHT are both: LEFT JOIN is a keyword, LEFT(ItemCode, 1) a function.
    private bool IsFunctionCall(int index)
    {
        var word = At(index).Text;
        return IsSymbol(index + 1, "(") && (!Keywords.Contains(word) || Functions.Contains(word));
    }

    private void WriteFunction(SqlToken token)
    {
        if (!Functions.Contains(token.Text))
        {
            throw Error(token.Start, $"Cannot support this function or expression: '{token.Text}'");
        }

        // The guide normalizes IsNull to IfNull on HANA (p.151). PostgreSQL has neither.
        if (token.Text.Equals("ISNULL", StringComparison.OrdinalIgnoreCase) || token.Text.Equals("IFNULL", StringComparison.OrdinalIgnoreCase))
        {
            _text.Append("IFNULL");
            _executable.Append("COALESCE");
        }
        else
        {
            WriteToBoth(token.Text);
        }
    }

    private string Qualifier(SqlToken token)
    {
        if (!_tablesByQualifier.ContainsKey(token.Text))
        {
            throw Error(token.Start, $"Incorrect syntax near '{token.Text}'.");
        }

        // A table name gets its exact spelling, an alias stays as written.
        var upper = token.Text.ToUpperInvariant();
        return _tables.Contains(upper) && !_tableAliases.Contains(token.Text) ? Quote(upper) : token.Text;
    }

    private string QualifiedColumn(SqlToken column, SqlToken qualifier)
    {
        var tables = _tablesByQualifier[qualifier.Text];

        return tables.Select(table => FindColumn(table, column.Text)).FirstOrDefault(name => name is not null)
            ?? throw SapErrors.SqlColumnNotAccessible(column.Text, tables[0]);
    }

    // A column of any table of the statement. PostgreSQL refuses it if two tables have it and it stays unclear.
    private string UnqualifiedName(SqlToken token)
    {
        foreach (var table in _tables)
        {
            if (FindColumn(table, token.Text) is { } column)
            {
                return column;
            }
        }

        // ORDER BY and GROUP BY may name an alias of the select list.
        return _columnAliases.FirstOrDefault(alias => alias.Equals(token.Text, StringComparison.OrdinalIgnoreCase))
            ?? throw SapErrors.SqlColumnNotAccessible(token.Text, _tables[0]);
    }

    private string? FindColumn(string table, string name) =>
        _columnsByTable[table].FirstOrDefault(column => column.Equals(name, StringComparison.OrdinalIgnoreCase));

    private void WriteToBoth(string text)
    {
        _text.Append(text);
        _executable.Append(text);
    }

    private SqlToken At(int index) =>
        index >= 0 && index < _tokens.Count ? _tokens[index] : new SqlToken(SqlTokenKind.End, string.Empty, _sql.Length, _sql.Length);

    private bool IsName(int index) => At(index).Kind is SqlTokenKind.Word or SqlTokenKind.QuotedName;

    private bool IsKeyword(int index, string word) =>
        At(index).Kind == SqlTokenKind.Word && At(index).Text.Equals(word, StringComparison.OrdinalIgnoreCase);

    private bool IsSymbol(int index, string symbol) => At(index).Kind == SqlTokenKind.Symbol && At(index).Text == symbol;

    private bool IsReserved(int index) =>
        At(index).Kind == SqlTokenKind.Word && (Keywords.Contains(At(index).Text) || NotSupported.Contains(At(index).Text));

    // Whether a value has just ended, so another name can only be an alias.
    private bool IsValueEnd(int index) => At(index).Kind switch
    {
        SqlTokenKind.QuotedName or SqlTokenKind.String or SqlTokenKind.Number or SqlTokenKind.Parameter => true,
        SqlTokenKind.Word => !IsReserved(index),
        SqlTokenKind.Symbol => At(index).Text == ")",
        _ => false,
    };

    // An alias is written into the query as it is, so a quoted one must not be able to carry SQL.
    private void EnsurePlainName(SqlToken token)
    {
        var isPlain = token.Text.Length > 0
            && (char.IsAsciiLetter(token.Text[0]) || token.Text[0] == '_')
            && token.Text.All(character => char.IsAsciiLetterOrDigit(character) || character == '_');

        if (!isPlain)
        {
            throw Error(token.Start, $"Incorrect syntax near '{token.Text}'.");
        }
    }

    private static string Quote(string name) => $"\"{name}\"";

    private ServiceLayerException Error(int position, string reason) => SapErrors.SqlSyntax(_sql, position, reason);
}
