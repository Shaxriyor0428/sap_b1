using MockSl.Application.OData;
using MockSl.Domain.Errors;
using MockSl.Domain.Metadata;

namespace MockSl.UnitTests.OData;

public class ExpandTests
{
    private static readonly EntityDefinition Invoices = EntityCatalog.Invoices;

    private static ODataQuery Parse(EntityDefinition entity, params (string Name, string Value)[] options) =>
        ODataQuery.Parse(entity, options.ToDictionary(option => option.Name, option => option.Value));

    private static string[] Names(IEnumerable<FieldDefinition> fields) => fields.Select(field => field.Name).ToArray();

    [Fact]
    public void The_example_of_the_task_selects_inside_and_outside_the_link()
    {
        var query = Parse(
            Invoices,
            ("$select", "DocEntry,DocNum,DocTotal"),
            ("$expand", "BusinessPartner($select=CardName,Phone1)"));

        Assert.Equal(["DocEntry", "DocNum", "DocTotal"], Names(query.Select));
        var expand = Assert.Single(query.Expand);
        Assert.Equal("BusinessPartner", expand.Navigation.Name);
        Assert.Equal(["CardName", "Phone1"], Names(expand.Select));
    }

    [Fact]
    public void Spaces_as_in_the_guide_are_allowed()
    {
        var query = Parse(Invoices, ("$expand", "BusinessPartner($select=CardName, Phone1)"));

        Assert.Equal(["CardName", "Phone1"], Names(query.Expand[0].Select));
    }

    [Fact]
    public void Without_select_every_field_of_the_linked_object_is_returned()
    {
        var query = Parse(Invoices, ("$expand", "BusinessPartner"));

        Assert.Equal(EntityCatalog.BusinessPartners.Fields, query.Expand[0].Select);
    }

    [Fact]
    public void The_link_may_be_named_in_select_as_in_the_guide()
    {
        var query = Parse(Invoices, ("$select", "*,BusinessPartner"), ("$expand", "BusinessPartner"));

        Assert.Equal(Invoices.Fields, query.Select);
        Assert.Single(query.Expand);
    }

    [Fact]
    public void Linked_fields_may_be_chosen_with_a_path_as_in_the_guide()
    {
        var query = Parse(Invoices, ("$select", "CardCode,BusinessPartner/CardName"), ("$expand", "BusinessPartner"));

        Assert.Equal(["CardCode"], Names(query.Select));
        Assert.Equal(["CardName"], Names(query.Expand[0].Select));
    }

    [Fact]
    public void A_path_without_expand_is_ignored_as_the_guide_says()
    {
        var query = Parse(Invoices, ("$select", "CardCode,BusinessPartner/CardName"));

        Assert.Equal(["CardCode"], Names(query.Select));
        Assert.Empty(query.Expand);
    }

    [Theory]
    [InlineData("Invoices")]
    [InlineData("PurchaseInvoices")]
    [InlineData("IncomingPayments")]
    public void The_objects_named_in_the_task_have_the_link(string setName)
    {
        var query = Parse(EntityCatalog.Find(setName)!, ("$expand", "BusinessPartner"));

        Assert.Single(query.Expand);
    }

    [Theory]
    [InlineData("BusinessPartners")]
    [InlineData("VendorPayments")]
    public void Other_objects_do_not_have_the_link(string setName)
    {
        var error = Assert.Throws<ServiceLayerException>(() =>
            Parse(EntityCatalog.Find(setName)!, ("$expand", "BusinessPartner")));

        Assert.Equal("-1000", error.Code);
    }

    [Theory]
    [InlineData("Nope", "Nope")]
    [InlineData("BusinessPartner($select=Nope)", "Nope")]
    public void Unknown_names_are_reported(string expand, string name)
    {
        var error = Assert.Throws<ServiceLayerException>(() => Parse(Invoices, ("$expand", expand)));

        Assert.Contains($"'{name}'", error.Message);
    }

    [Theory]
    [InlineData("Nope/CardName")]
    [InlineData("BusinessPartner/Nope")]
    [InlineData("BusinessPartner/CardName/Extra")]
    public void Unknown_paths_are_reported(string select)
    {
        Assert.Throws<ServiceLayerException>(() =>
            Parse(Invoices, ("$select", select), ("$expand", "BusinessPartner")));
    }

    [Theory]
    [InlineData("")]
    [InlineData(",")]
    [InlineData("BusinessPartner,")]
    [InlineData("BusinessPartner(")]
    [InlineData("BusinessPartner)")]
    [InlineData("BusinessPartner()")]
    [InlineData("BusinessPartner($select)")]
    [InlineData("BusinessPartner($select=)")]
    [InlineData("BusinessPartner($select=CardName")]
    [InlineData("BusinessPartner($select=CardName,)")]
    [InlineData("BusinessPartner BusinessPartner")]
    [InlineData("BusinessPartner($select=CardName\";DROP TABLE \"OCRD)")]
    [InlineData("BusinessPartner;DROP TABLE \"OCRD\"")]
    public void Broken_text_is_refused(string expand)
    {
        var error = Assert.Throws<ServiceLayerException>(() => Parse(Invoices, ("$expand", expand)));

        Assert.Equal("-1000", error.Code);
    }

    [Fact]
    public void Only_select_is_supported_inside_the_brackets()
    {
        var error = Assert.Throws<ServiceLayerException>(() =>
            Parse(Invoices, ("$expand", "BusinessPartner($filter=CardName eq 'x')")));

        Assert.Contains("$filter", error.Message);
    }
}
