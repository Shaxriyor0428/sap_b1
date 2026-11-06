using MockSl.Application.SqlQueries;
using MockSl.Domain.Errors;

namespace MockSl.UnitTests.SqlQueries;

public class ParamListParserTests
{
    [Fact]
    public void The_example_of_the_task_is_read()
    {
        var parameter = Assert.Single(ParamListParser.Parse("fromDate='2026-10-01'"));

        Assert.Equal(new SqlParameter("fromDate", "'2026-10-01'", "2026-10-01"), parameter);
    }

    [Fact]
    public void Several_parameters_are_separated_as_in_the_guide()
    {
        var parameters = ParamListParser.Parse("stringParam1='val1'&integerParam2=10.1");

        Assert.Equal(
            [new SqlParameter("stringParam1", "'val1'", "val1"), new SqlParameter("integerParam2", "10.1", "10.1")],
            parameters);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Nothing_given_means_no_parameters(string? paramList)
    {
        Assert.Empty(ParamListParser.Parse(paramList));
    }

    [Fact]
    public void A_doubled_quote_is_one_quote_in_the_value()
    {
        Assert.Equal("O'Brien", ParamListParser.Parse("name='O''Brien'")[0].Value);
    }

    [Fact]
    public void An_ampersand_inside_a_string_is_part_of_the_value()
    {
        Assert.Equal("a&b=c", Assert.Single(ParamListParser.Parse("name='a&b=c'")).Value);
    }

    [Theory]
    [InlineData("-5")]
    [InlineData("0")]
    [InlineData("12.50")]
    public void Numbers_need_no_quotes(string number)
    {
        Assert.Equal(number, ParamListParser.Parse($"a={number}")[0].Value);
    }

    [Theory]
    [InlineData("itemCode='i001';truncate table OITM")]
    [InlineData("itemCode='i001' or 1=1")]
    [InlineData("itemCode=i001")]
    [InlineData("itemCode=1;drop table OITM")]
    [InlineData("itemCode='open")]
    [InlineData("itemCode")]
    [InlineData("itemCode=")]
    [InlineData("='x'")]
    [InlineData("a='x'&")]
    [InlineData("a='x'&&b=1")]
    [InlineData("a=1.2.3")]
    [InlineData("a=1.")]
    [InlineData("a=-")]
    [InlineData("a b='x'")]
    [InlineData("fromDate=2026-10-01")]
    public void Anything_else_gets_the_parameter_error_of_the_guide(string paramList)
    {
        var error = Assert.Throws<ServiceLayerException>(() => ParamListParser.Parse(paramList));

        Assert.Equal("704", error.Code);
        Assert.Equal("Parameter error.", error.Message);
    }

    [Fact]
    public void A_query_string_gives_the_same_parameters()
    {
        var parameters = ParamListParser.FromQuery([KeyValuePair.Create("docTotal", "10.1"), KeyValuePair.Create("code", "'a'")]);

        Assert.Equal(
            [new SqlParameter("docTotal", "10.1", "10.1"), new SqlParameter("code", "'a'", "a")],
            parameters);
    }

    [Theory]
    [InlineData("code", "'a';drop table OITM")]
    [InlineData("code", "a")]
    [InlineData("co de", "'a'")]
    [InlineData("code", "")]
    public void A_bad_query_string_gets_the_same_error(string name, string value)
    {
        var error = Assert.Throws<ServiceLayerException>(() => ParamListParser.FromQuery([KeyValuePair.Create(name, value)]));

        Assert.Equal("704", error.Code);
    }
}
