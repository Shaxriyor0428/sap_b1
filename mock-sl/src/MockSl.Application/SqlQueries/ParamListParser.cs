using MockSl.Domain.Errors;

namespace MockSl.Application.SqlQueries;

/// <param name="Raw">As the client wrote it, for the next page link.</param>
/// <param name="Value">What is bound to the query: without quotes, never added to the SQL text.</param>
public sealed record SqlParameter(string Name, string Raw, string Value);

/// <summary>
/// Reads parameter values (guide p.152-153): stringParam1='val1'&amp;integerParam2=val2.
/// Anything else after a value is the injection attempt from guide p.159-160, and gets its 704 error:
/// itemCode='i001';truncate table OITM
/// </summary>
public static class ParamListParser
{
    /// <summary>The text from the body of POST List.</summary>
    public static IReadOnlyList<SqlParameter> Parse(string? paramList)
    {
        var parameters = new List<SqlParameter>();

        if (string.IsNullOrWhiteSpace(paramList))
        {
            return parameters;
        }

        var index = 0;

        while (true)
        {
            var name = ReadName(paramList, ref index);
            Expect(paramList, ref index, '=');

            var start = index;
            var value = ReadValue(paramList, ref index);
            parameters.Add(new SqlParameter(name, paramList[start..index], value));

            if (index == paramList.Length)
            {
                return parameters;
            }

            Expect(paramList, ref index, '&');
        }
    }

    /// <summary>The query string of GET List, already split by the web server.</summary>
    public static IReadOnlyList<SqlParameter> FromQuery(IEnumerable<KeyValuePair<string, string>> pairs)
    {
        var parameters = new List<SqlParameter>();

        foreach (var (name, raw) in pairs)
        {
            var nameIndex = 0;
            ReadName(name, ref nameIndex);

            var valueIndex = 0;
            var value = ReadValue(raw, ref valueIndex);

            if (nameIndex != name.Length || valueIndex != raw.Length)
            {
                throw SapErrors.SqlParameterError();
            }

            parameters.Add(new SqlParameter(name, raw, value));
        }

        return parameters;
    }

    private static string ReadName(string text, ref int index)
    {
        var start = index;

        while (index < text.Length && (char.IsAsciiLetterOrDigit(text[index]) || text[index] == '_'))
        {
            index++;
        }

        return index > start ? text[start..index] : throw SapErrors.SqlParameterError();
    }

    private static string ReadValue(string text, ref int index) =>
        index < text.Length && text[index] == '\'' ? ReadString(text, ref index) : ReadNumber(text, ref index);

    private static string ReadString(string text, ref int index)
    {
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
                return value.ToString();
            }
        }

        throw SapErrors.SqlParameterError();
    }

    private static string ReadNumber(string text, ref int index)
    {
        var start = index;

        if (index < text.Length && text[index] == '-')
        {
            index++;
        }

        var digitsStart = index;
        while (index < text.Length && (char.IsAsciiDigit(text[index]) || text[index] == '.'))
        {
            index++;
        }

        var digits = text[digitsStart..index];
        var isNumber = digits.Length > 0 && digits.Count(character => character == '.') <= 1 && digits[0] != '.' && digits[^1] != '.';

        return isNumber ? text[start..index] : throw SapErrors.SqlParameterError();
    }

    private static void Expect(string text, ref int index, char character)
    {
        if (index >= text.Length || text[index] != character)
        {
            throw SapErrors.SqlParameterError();
        }

        index++;
    }
}
