using MockSl.Application.OData;
using MockSl.Domain.Errors;
using MockSl.Domain.Metadata;

namespace MockSl.UnitTests.OData;

public class ODataQueryTests
{
    private static ODataQuery Parse(params (string Name, string Value)[] options) =>
        ODataQuery.Parse(
            EntityCatalog.BusinessPartners,
            options.ToDictionary(option => option.Name, option => option.Value));

    [Fact]
    public void Without_select_every_field_is_returned()
    {
        Assert.Equal(EntityCatalog.BusinessPartners.Fields, Parse().Select);
    }

    [Fact]
    public void Star_selects_every_field()
    {
        Assert.Equal(EntityCatalog.BusinessPartners.Fields, Parse(("$select", "*")).Select);
    }

    [Fact]
    public void Select_lists_fields_in_the_requested_order()
    {
        var select = Parse(("$select", "Phone1, CardCode")).Select;

        Assert.Equal(["Phone1", "CardCode"], select.Select(field => field.Name));
    }

    [Theory]
    [InlineData("$select", "CardCode,Foo")]
    [InlineData("$orderby", "Foo desc")]
    public void Unknown_field_is_rejected(string option, string value)
    {
        var error = Assert.Throws<ServiceLayerException>(() => Parse((option, value)));

        Assert.Contains("'Foo'", error.Message);
    }

    [Fact]
    public void OrderBy_reads_direction_per_field()
    {
        var orderBy = Parse(("$orderby", "CardName desc, CardCode asc, City")).OrderBy;

        Assert.Equal(
            [("CardName", true), ("CardCode", false), ("City", false)],
            orderBy.Select(item => (item.Field.Name, item.Descending)));
    }

    [Fact]
    public void OrderBy_with_an_unknown_direction_is_rejected()
    {
        Assert.Throws<ServiceLayerException>(() => Parse(("$orderby", "CardName sideways")));
    }

    [Fact]
    public void Top_and_skip_are_read()
    {
        var query = Parse(("$top", "20"), ("$skip", "40"));

        Assert.Equal(20, query.Top);
        Assert.Equal(40, query.Skip);
    }

    [Theory]
    [InlineData("$top", "-1")]
    [InlineData("$top", "abc")]
    [InlineData("$skip", "1.5")]
    public void Top_and_skip_must_be_non_negative_integers(string option, string value)
    {
        Assert.Throws<ServiceLayerException>(() => Parse((option, value)));
    }

    [Theory]
    [InlineData("$expand")]
    [InlineData("$apply")]
    public void Options_that_are_not_implemented_are_rejected(string option)
    {
        var error = Assert.Throws<ServiceLayerException>(() => Parse((option, "x")));

        Assert.Contains(option, error.Message);
    }

    [Fact]
    public void Ordinary_query_parameters_are_ignored()
    {
        Assert.Null(Parse(("foo", "bar")).Filter);
    }
}
