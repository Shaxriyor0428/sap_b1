using System.Text.Json;
using MockSl.Application.Entities;
using MockSl.Domain.Errors;
using MockSl.Domain.Metadata;

namespace MockSl.UnitTests.Entities;

public class EntityBodyReaderTests
{
    private static readonly EntityDefinition Entity = EntityCatalog.BusinessPartners;

    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement;

    private static object? ValueOf(IEnumerable<FieldValue> values, string fieldName) =>
        values.Single(value => value.Field.Name == fieldName).Value;

    [Theory]
    [InlineData("cCustomer")]
    [InlineData("C")]
    public void Enum_is_stored_as_its_database_value(string cardType)
    {
        var values = EntityBodyReader.ReadForCreate(Entity, Json($$"""{"CardCode":"c1","CardType":"{{cardType}}"}"""));

        Assert.Equal("C", ValueOf(values, "CardType"));
    }

    [Fact]
    public void Missing_required_field_is_rejected_on_create()
    {
        var error = Assert.Throws<ServiceLayerException>(() =>
            EntityBodyReader.ReadForCreate(Entity, Json("""{"CardName":"x"}""")));

        Assert.Equal("-10", error.Code);
        Assert.Contains("CardCode", error.Message);
    }

    [Theory]
    [InlineData("""{"CardCode":""}""")]
    [InlineData("""{"CardCode":null}""")]
    public void Empty_required_field_is_rejected_on_create(string body)
    {
        Assert.Throws<ServiceLayerException>(() => EntityBodyReader.ReadForCreate(Entity, Json(body)));
    }

    [Fact]
    public void Unknown_property_is_rejected()
    {
        var error = Assert.Throws<ServiceLayerException>(() =>
            EntityBodyReader.ReadForUpdate(Entity, Json("""{"Foo":"x"}""")));

        Assert.Contains("'Foo'", error.Message);
    }

    [Fact]
    public void Database_column_name_is_not_accepted_in_place_of_the_service_layer_name()
    {
        Assert.Throws<ServiceLayerException>(() =>
            EntityBodyReader.ReadForUpdate(Entity, Json("""{"E_Mail":"a@b.uz"}""")));
    }

    [Fact]
    public void Read_only_field_is_ignored_silently()
    {
        var values = EntityBodyReader.ReadForCreate(Entity, Json("""{"CardCode":"c1","CurrentAccountBalance":999}"""));

        Assert.DoesNotContain(values, value => value.Field.Name == "CurrentAccountBalance");
    }

    [Fact]
    public void Key_is_ignored_silently_on_update_but_kept_on_create()
    {
        var body = Json("""{"CardCode":"c2","CardName":"x"}""");

        Assert.DoesNotContain(EntityBodyReader.ReadForUpdate(Entity, body), value => value.Field.Name == "CardCode");
        Assert.Contains(EntityBodyReader.ReadForCreate(Entity, body), value => value.Field.Name == "CardCode");
    }

    [Fact]
    public void Value_longer_than_the_column_is_rejected()
    {
        var body = $$"""{"CardCode":"{{new string('x', 16)}}"}""";

        var error = Assert.Throws<ServiceLayerException>(() => EntityBodyReader.ReadForCreate(Entity, Json(body)));

        Assert.Contains("too long", error.Message);
    }

    [Theory]
    [InlineData("""{"CardName":123}""")]
    [InlineData("""{"CardType":"cNothing"}""")]
    public void Value_of_the_wrong_type_is_rejected(string body)
    {
        var error = Assert.Throws<ServiceLayerException>(() => EntityBodyReader.ReadForUpdate(Entity, Json(body)));

        Assert.Contains("Invalid value", error.Message);
    }

    [Fact]
    public void Null_clears_an_optional_field()
    {
        var values = EntityBodyReader.ReadForUpdate(Entity, Json("""{"Phone1":null}"""));

        Assert.Null(ValueOf(values, "Phone1"));
    }

    [Fact]
    public void Repeated_property_keeps_only_the_last_value()
    {
        var values = EntityBodyReader.ReadForUpdate(Entity, Json("""{"CardName":"first","CardName":"last"}"""));

        Assert.Equal("last", Assert.Single(values).Value);
    }

    [Fact]
    public void Odata_annotations_are_skipped()
    {
        var values = EntityBodyReader.ReadForUpdate(Entity, Json("""{"@odata.context":"x","CardName":"n"}"""));

        Assert.Equal("CardName", Assert.Single(values).Field.Name);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("42")]
    public void Body_must_be_an_object(string body)
    {
        var error = Assert.Throws<ServiceLayerException>(() => EntityBodyReader.ReadForUpdate(Entity, Json(body)));

        Assert.Equal("The request body is not a valid JSON object", error.Message);
    }
}
