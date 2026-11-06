using MockSl.Application.OData;
using MockSl.Domain.Errors;
using MockSl.Domain.Metadata;

namespace MockSl.UnitTests.OData;

public class FilterParserTests
{
    private static FilterNode Parse(string filter) => FilterParser.Parse(EntityCatalog.BusinessPartners, filter);

    [Theory]
    [InlineData("CardType eq 'cCustomer'")]
    [InlineData("CardType eq 'C'")]
    public void Enum_is_accepted_by_name_and_by_database_value(string filter)
    {
        var node = Assert.IsType<ComparisonNode>(Parse(filter));

        Assert.Equal("CardType", node.Field.Name);
        Assert.Equal(ComparisonOperator.Equal, node.Operator);
        Assert.Equal("C", node.Value);
    }

    [Fact]
    public void Field_keeps_its_service_layer_name_and_database_column()
    {
        var node = Assert.IsType<ComparisonNode>(Parse("EmailAddress eq 'a@b.uz'"));

        Assert.Equal("EmailAddress", node.Field.Name);
        Assert.Equal("E_Mail", node.Field.Column);
    }

    [Fact]
    public void And_binds_tighter_than_or()
    {
        var node = Assert.IsType<OrNode>(Parse("CardCode eq 'A' or CardCode eq 'B' and City eq 'C'"));

        Assert.IsType<ComparisonNode>(node.Left);
        Assert.IsType<AndNode>(node.Right);
    }

    [Fact]
    public void Parentheses_override_precedence()
    {
        var node = Assert.IsType<AndNode>(Parse("(CardCode eq 'A' or CardCode eq 'B') and City eq 'C'"));

        Assert.IsType<OrNode>(node.Left);
        Assert.IsType<ComparisonNode>(node.Right);
    }

    [Fact]
    public void Not_wraps_the_following_expression()
    {
        var node = Assert.IsType<NotNode>(Parse("not (startswith(CardName,'A') and City eq null)"));

        Assert.IsType<AndNode>(node.Inner);
    }

    [Theory]
    [InlineData("contains(CardName,'Ali')", TextMatchKind.Contains)]
    [InlineData("startswith(CardName,'Ali')", TextMatchKind.StartsWith)]
    [InlineData("endswith(CardName,'Ali')", TextMatchKind.EndsWith)]
    [InlineData("substringof('Ali',CardName)", TextMatchKind.Contains)]
    public void Text_functions_are_recognised(string filter, TextMatchKind expected)
    {
        var node = Assert.IsType<TextMatchNode>(Parse(filter));

        Assert.Equal(expected, node.Kind);
        Assert.Equal("CardName", node.Field.Name);
        Assert.Equal("Ali", node.Text);
    }

    [Fact]
    public void Doubled_quote_is_read_as_one_apostrophe()
    {
        var node = Assert.IsType<ComparisonNode>(Parse("CardName eq 'O''Brien'"));

        Assert.Equal("O'Brien", node.Value);
    }

    [Theory]
    [InlineData("City eq null", ComparisonOperator.Equal)]
    [InlineData("City ne null", ComparisonOperator.NotEqual)]
    public void Null_is_allowed_with_eq_and_ne(string filter, ComparisonOperator expected)
    {
        var node = Assert.IsType<ComparisonNode>(Parse(filter));

        Assert.Equal(expected, node.Operator);
        Assert.Null(node.Value);
    }

    [Fact]
    public void Numbers_become_decimals_for_decimal_fields()
    {
        var node = Assert.IsType<ComparisonNode>(Parse("CurrentAccountBalance gt 1500.5"));

        Assert.Equal(1500.5m, node.Value);
    }

    [Theory]
    [InlineData("Foo eq 'x'", "Foo")]
    [InlineData("contains(Foo,'x')", "Foo")]
    [InlineData("E_Mail eq 'x'", "E_Mail")]
    public void Unknown_field_is_rejected_by_name(string filter, string field)
    {
        var error = Assert.Throws<ServiceLayerException>(() => Parse(filter));

        Assert.Equal("-1000", error.Code);
        Assert.Contains($"'{field}'", error.Message);
    }

    [Theory]
    [InlineData("CardCode eq")]
    [InlineData("CardCode 'x'")]
    [InlineData("CardCode like 'x'")]
    [InlineData("(CardCode eq 'x'")]
    [InlineData("CardCode eq 'x')")]
    [InlineData("CardCode eq 'x")]
    [InlineData("CardCode eq 'x' and")]
    [InlineData("CardCode gt null")]
    [InlineData("foo(CardName,'x')")]
    [InlineData("contains(CardName)")]
    [InlineData("contains(CurrentAccountBalance,'1')")]
    public void Bad_syntax_is_rejected(string filter)
    {
        var error = Assert.Throws<ServiceLayerException>(() => Parse(filter));

        Assert.Equal("-1000", error.Code);
        Assert.StartsWith("Invalid $filter", error.Message);
    }

    [Theory]
    [InlineData("CardCode eq 1")]
    [InlineData("CurrentAccountBalance eq 'abc'")]
    [InlineData("CardType eq 'cNothing'")]
    public void Value_of_the_wrong_type_is_rejected(string filter)
    {
        var error = Assert.Throws<ServiceLayerException>(() => Parse(filter));

        Assert.Contains("Invalid value", error.Message);
    }

    [Theory]
    [InlineData("CardCode eq 'x'; DROP TABLE \"OCRD\"")]
    [InlineData("\"CardCode\" eq 'x'")]
    [InlineData("CardCode eq 'x' -- comment")]
    [InlineData("1 eq 1")]
    public void Sql_outside_a_string_literal_is_a_syntax_error(string filter)
    {
        var error = Assert.Throws<ServiceLayerException>(() => Parse(filter));

        Assert.StartsWith("Invalid $filter", error.Message);
    }

    [Fact]
    public void Very_deep_nesting_is_rejected_instead_of_exhausting_the_stack()
    {
        var filter = new string('(', 5000) + "City eq 'x'" + new string(')', 5000);

        var error = Assert.Throws<ServiceLayerException>(() => Parse(filter));

        Assert.Contains("nested too deeply", error.Message);
    }
}
