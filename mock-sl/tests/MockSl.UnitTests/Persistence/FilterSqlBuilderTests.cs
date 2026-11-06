using MockSl.Application.OData;
using MockSl.Domain.Metadata;
using MockSl.Infrastructure.Persistence;

namespace MockSl.UnitTests.Persistence;

public class FilterSqlBuilderTests
{
    private static (string Sql, IReadOnlyList<SqlParameterValue> Parameters) Build(string filter)
    {
        var parameters = new ParameterBag();
        var sql = FilterSqlBuilder.Build(FilterParser.Parse(EntityCatalog.BusinessPartners, filter), parameters);
        return (sql, parameters.Values);
    }

    [Theory]
    [InlineData("City eq 'x'", "\"City\" = @p0")]
    [InlineData("City ne 'x'", "\"City\" <> @p0")]
    [InlineData("CurrentAccountBalance gt 1", "\"Balance\" > @p0")]
    [InlineData("CurrentAccountBalance ge 1", "\"Balance\" >= @p0")]
    [InlineData("CurrentAccountBalance lt 1", "\"Balance\" < @p0")]
    [InlineData("CurrentAccountBalance le 1", "\"Balance\" <= @p0")]
    public void Comparison_uses_the_database_column_and_a_parameter(string filter, string expected)
    {
        Assert.Equal(expected, Build(filter).Sql);
    }

    [Fact]
    public void Service_layer_name_is_translated_to_the_column_name()
    {
        Assert.Equal("\"E_Mail\" = @p0", Build("EmailAddress eq 'a@b.uz'").Sql);
    }

    [Fact]
    public void Logic_keeps_its_grouping()
    {
        var (sql, parameters) = Build("(City eq 'a' or City eq 'b') and not (Phone1 eq null)");

        Assert.Equal("((\"City\" = @p0 OR \"City\" = @p1) AND NOT (\"Phone1\" IS NULL))", sql);
        Assert.Equal(2, parameters.Count);
    }

    [Fact]
    public void Null_comparison_needs_no_parameter()
    {
        var (sql, parameters) = Build("City ne null");

        Assert.Equal("\"City\" IS NOT NULL", sql);
        Assert.Empty(parameters);
    }

    [Theory]
    [InlineData("contains(CardName,'Ali')", "%Ali%")]
    [InlineData("startswith(CardName,'Ali')", "Ali%")]
    [InlineData("endswith(CardName,'Ali')", "%Ali")]
    public void Text_functions_become_like_patterns(string filter, string expectedPattern)
    {
        var (sql, parameters) = Build(filter);

        Assert.Equal("\"CardName\" LIKE @p0 ESCAPE '\\'", sql);
        Assert.Equal(expectedPattern, Assert.Single(parameters).Value);
    }

    [Theory]
    [InlineData("50%", "%50\\%%")]
    [InlineData("a_b", "%a\\_b%")]
    [InlineData("a\\b", "%a\\\\b%")]
    public void Like_wildcards_in_user_text_are_escaped(string text, string expectedPattern)
    {
        var (_, parameters) = Build($"contains(CardName,'{text}')");

        Assert.Equal(expectedPattern, Assert.Single(parameters).Value);
    }

    [Theory]
    [InlineData("x'' OR ''1''=''1", "x' OR '1'='1")]
    [InlineData("x''; DROP TABLE \"OCRD\"; --", "x'; DROP TABLE \"OCRD\"; --")]
    [InlineData("'' UNION SELECT \"PASSWORD\" FROM \"OUSR\" --", "' UNION SELECT \"PASSWORD\" FROM \"OUSR\" --")]
    public void Injection_attempts_stay_in_the_parameter_and_never_reach_the_sql(string literal, string expectedValue)
    {
        var (sql, parameters) = Build($"CardName eq '{literal}'");

        Assert.Equal("\"CardName\" = @p0", sql);
        Assert.Equal(expectedValue, Assert.Single(parameters).Value);
    }

    [Fact]
    public void Enum_reaches_the_parameter_as_its_database_value()
    {
        Assert.Equal("C", Assert.Single(Build("CardType eq 'cCustomer'").Parameters).Value);
    }
}
