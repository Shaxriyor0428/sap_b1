using System.Text.RegularExpressions;

namespace GroceryStore.UnitTests;

/// <summary>
/// The rules of the sql folder that can be checked without a database, so CI runs them too.
/// The results themselves are compared on the mock database by sql/check.sh.
/// </summary>
public class SqlFilesTests
{
    private static readonly string SqlFolder = FindSqlFolder();

    public static TheoryData<string> AllFiles()
    {
        var files = new TheoryData<string>();
        foreach (var path in Directory.GetFiles(SqlFolder, "*.sql"))
        {
            files.Add(Path.GetFileName(path));
        }

        return files;
    }

    [Fact]
    public void There_is_one_file_per_task()
    {
        var numbers = Directory.GetFiles(SqlFolder, "*.sql").Select(path => Path.GetFileName(path)[..2]).Order();

        Assert.Equal(Enumerable.Range(1, 14).Select(number => number.ToString("00", null)), numbers);
    }

    [Theory]
    [MemberData(nameof(AllFiles))]
    public void Only_syntax_that_also_works_on_hana_is_used(string file)
    {
        var code = Code(file);

        Assert.DoesNotContain("::", code);
        Assert.DoesNotContain("ILIKE", code, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(AllFiles))]
    public void Tables_and_columns_are_quoted(string file)
    {
        var code = Code(file);

        Assert.DoesNotMatch(@"(?i)\b(FROM|JOIN)\s+[^""\s]", code);
        Assert.DoesNotMatch(@"\bT\d\.[^""]", code);
    }

    [Theory]
    [MemberData(nameof(AllFiles))]
    public void Cancelled_documents_are_left_out(string file)
    {
        var code = Code(file);

        Assert.Equal(Count(code, @"""OINV"" |""OPCH"" "), Count(code, @"""CANCELED"" = 'N'"));
        Assert.Equal(Count(code, @"""ORCT"" |""OVPM"" "), Count(code, @"""Canceled"" = 'N'"));
    }

    [Theory]
    [MemberData(nameof(AllFiles))]
    public void Database_values_are_used_and_not_service_layer_names(string file)
    {
        Assert.DoesNotMatch("cCustomer|cSupplier|tYES|tNO|bost_", Code(file));
    }

    [Theory]
    [MemberData(nameof(AllFiles))]
    public void Dates_are_static(string file)
    {
        Assert.DoesNotMatch(@"(?i)CURRENT_DATE|CURRENT_TIMESTAMP|NOW\s*\(", Code(file));
    }

    [Theory]
    [MemberData(nameof(AllFiles))]
    public void The_file_starts_with_the_text_of_its_task(string file)
    {
        var number = int.Parse(file[..2], null);

        Assert.StartsWith($"-- Task {number}. ", File.ReadAllText(Path.Combine(SqlFolder, file)));
    }

    [Theory]
    [MemberData(nameof(AllFiles))]
    public void Parameters_are_only_in_the_query_manager_file(string file)
    {
        var code = Code(file);

        if (file.StartsWith("14", StringComparison.Ordinal))
        {
            Assert.Contains("[%0]", code);
            Assert.Contains("[%1]", code);
            Assert.DoesNotMatch(@"'\d{4}-\d{2}-\d{2}'", code);
            Assert.DoesNotContain("-- > ", File.ReadAllText(Path.Combine(SqlFolder, file)));
        }
        else
        {
            Assert.DoesNotContain("[%", code);
            Assert.Contains("-- > ", File.ReadAllText(Path.Combine(SqlFolder, file)));
        }
    }

    private static int Count(string text, string pattern) => Regex.Matches(text, pattern).Count;

    // The rules apply to the query, not to the comments that explain it.
    private static string Code(string file) => string.Join(
        '\n',
        File.ReadAllLines(Path.Combine(SqlFolder, file)).Where(line => !line.StartsWith("--", StringComparison.Ordinal)));

    private static string FindSqlFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SapB1.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException("SapB1.slnx not found"), "sql");
    }
}
