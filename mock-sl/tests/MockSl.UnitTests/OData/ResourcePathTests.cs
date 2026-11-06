using MockSl.Application.Entities;
using MockSl.Application.OData;
using MockSl.Domain.Errors;
using MockSl.Domain.Metadata;

namespace MockSl.UnitTests.OData;

public class ResourcePathTests
{
    [Fact]
    public void Collection_has_no_key()
    {
        var path = ResourcePath.Parse("BusinessPartners");

        Assert.Equal("BusinessPartners", path.EntitySet);
        Assert.Null(path.Key);
        Assert.Null(path.Action);
    }

    [Fact]
    public void Short_text_key_is_read()
    {
        var part = Assert.Single(ResourcePath.Parse("BusinessPartners('C0001')").Key!);

        Assert.Null(part.Name);
        Assert.Equal("C0001", part.Value.Text);
    }

    [Fact]
    public void Named_key_is_read()
    {
        var part = Assert.Single(ResourcePath.Parse("BusinessPartners(CardCode='C0001')").Key!);

        Assert.Equal("CardCode", part.Name);
        Assert.Equal("C0001", part.Value.Text);
    }

    [Fact]
    public void Numeric_key_and_action_are_read()
    {
        var path = ResourcePath.Parse("Invoices(22)/Cancel");

        Assert.Equal(TokenKind.Number, Assert.Single(path.Key!).Value.Kind);
        Assert.Equal("Cancel", path.Action);
    }

    [Fact]
    public void Composite_key_is_read_with_spaces()
    {
        var key = ResourcePath.Parse("UserFieldsMD(TableName='OCRD', FieldID=0)").Key!;

        Assert.Equal(["TableName", "FieldID"], key.Select(part => part.Name));
    }

    [Theory]
    [InlineData("")]
    [InlineData("BusinessPartners(")]
    [InlineData("BusinessPartners()")]
    [InlineData("BusinessPartners(C0001)")]
    [InlineData("BusinessPartners('C0001'")]
    [InlineData("BusinessPartners('C0001') extra")]
    public void Malformed_path_is_rejected(string path)
    {
        var error = Assert.Throws<ServiceLayerException>(() => ResourcePath.Parse(path));

        Assert.Equal(ServiceLayerErrorKind.NotFound, error.Kind);
    }

    [Theory]
    [InlineData("BusinessPartners('c1')")]
    [InlineData("BusinessPartners(CardCode='c1')")]
    public void Both_key_forms_bind_to_the_same_field(string url)
    {
        var entity = EntityCatalog.BusinessPartners;

        var value = Assert.Single(EntityKey.Bind(entity, ResourcePath.Parse(url).Key!));

        Assert.Equal(entity.Key[0], value.Field);
        Assert.Equal("c1", value.Value);
    }

    [Theory]
    [InlineData("BusinessPartners(1)")] // the guide requires quotes around text keys (p.26)
    [InlineData("BusinessPartners(CardName='c1')")]
    [InlineData("BusinessPartners('a','b')")]
    public void Key_that_does_not_fit_the_object_is_rejected(string url)
    {
        var parts = ResourcePath.Parse(url).Key!;

        Assert.Throws<ServiceLayerException>(() => EntityKey.Bind(EntityCatalog.BusinessPartners, parts));
    }

    [Theory]
    [InlineData("c1", "('c1')")]
    [InlineData("O'Brien", "('O''Brien')")]
    public void Key_is_written_back_for_the_location_header(string code, string expected)
    {
        var row = new EntityRow(new Dictionary<string, object?> { ["CardCode"] = code });

        Assert.Equal(expected, EntityKey.Format(EntityCatalog.BusinessPartners, row));
    }
}
