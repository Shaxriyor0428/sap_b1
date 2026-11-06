using System.Text.Json;
using MockSl.Application.Entities;
using MockSl.Domain.Errors;
using MockSl.Domain.Metadata;

namespace MockSl.UnitTests.Entities;

public class EntityBodyReaderTests
{
    private static readonly EntityDefinition Entity = EntityCatalog.BusinessPartners;
    private static readonly EntityDefinition Invoices = EntityCatalog.Invoices;

    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement;

    private static object? ValueOf(IEnumerable<FieldValue> values, string fieldName) =>
        values.Single(value => value.Field.Name == fieldName).Value;

    [Theory]
    [InlineData("cCustomer")]
    [InlineData("C")]
    public void Enum_is_stored_as_its_database_value(string cardType)
    {
        var draft = EntityBodyReader.ReadForCreate(Entity, Json($$"""{"CardCode":"c1","CardType":"{{cardType}}"}"""));

        Assert.Equal("C", ValueOf(draft.Header.Values, "CardType"));
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
        var draft = EntityBodyReader.ReadForCreate(Entity, Json("""{"CardCode":"c1","CurrentAccountBalance":999}"""));

        Assert.DoesNotContain(draft.Header.Values, value => value.Field.Name == "CurrentAccountBalance");
    }

    [Fact]
    public void Key_is_ignored_silently_on_update_but_kept_on_create()
    {
        var body = Json("""{"CardCode":"c2","CardName":"x"}""");

        Assert.DoesNotContain(EntityBodyReader.ReadForUpdate(Entity, body).Values, value => value.Field.Name == "CardCode");
        Assert.Contains(EntityBodyReader.ReadForCreate(Entity, body).Header.Values, value => value.Field.Name == "CardCode");
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
        var changes = EntityBodyReader.ReadForUpdate(Entity, Json("""{"Phone1":null}"""));

        Assert.Null(ValueOf(changes.Values, "Phone1"));
    }

    [Fact]
    public void Repeated_property_keeps_only_the_last_value()
    {
        var changes = EntityBodyReader.ReadForUpdate(Entity, Json("""{"CardName":"first","CardName":"last"}"""));

        Assert.Equal("last", Assert.Single(changes.Values).Value);
    }

    [Fact]
    public void Odata_annotations_are_skipped()
    {
        var changes = EntityBodyReader.ReadForUpdate(Entity, Json("""{"@odata.context":"x","CardName":"n"}"""));

        Assert.Equal("CardName", Assert.Single(changes.Values).Field.Name);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("42")]
    public void Body_must_be_an_object(string body)
    {
        var error = Assert.Throws<ServiceLayerException>(() => EntityBodyReader.ReadForUpdate(Entity, Json(body)));

        Assert.Equal("The request body is not a valid JSON object", error.Message);
    }

    [Fact]
    public void Document_lines_become_rows_with_database_columns()
    {
        var draft = EntityBodyReader.ReadForCreate(Invoices, Json("""
            {"CardCode":"c1","DocDate":"2026-11-01","DocumentLines":[
              {"ItemCode":"i1","Quantity":2,"UnitPrice":100,"WarehouseCode":"01"},
              {"ItemCode":"i2","Quantity":"3","Price":50}
            ]}
            """));

        var lines = draft.Rows(Invoices.Child("DocumentLines"));

        Assert.Equal(2, lines.Count);
        Assert.Equal("i1", lines[0].Text("ItemCode"));
        Assert.Equal(100m, lines[0].Decimal("Price"));
        Assert.Equal("01", lines[0].Text("WhsCode"));
        Assert.Equal(3m, lines[1].Decimal("Quantity"));
        Assert.Equal(new DateOnly(2026, 11, 1), draft.Header.Date("DocDate"));
    }

    [Fact]
    public void Price_and_unit_price_share_one_column_and_the_last_wins()
    {
        var draft = EntityBodyReader.ReadForCreate(Invoices, Json("""
            {"CardCode":"c1","DocumentLines":[{"ItemCode":"i1","Quantity":1,"Price":10,"UnitPrice":20}]}
            """));

        var line = Assert.Single(draft.Rows(Invoices.Child("DocumentLines")));

        Assert.Equal(20m, line.Decimal("Price"));
        Assert.Single(line.Values, value => value.Field.Column == "Price");
    }

    [Fact]
    public void Totals_sent_by_the_client_are_ignored()
    {
        var draft = EntityBodyReader.ReadForCreate(Invoices, Json("""
            {"CardCode":"c1","DocTotal":1,"PaidToDate":1,"DocumentStatus":"bost_Close","Cancelled":"tYES",
             "DocumentLines":[{"ItemCode":"i1","Quantity":1,"Price":10,"LineTotal":1,"LineNum":7}]}
            """));

        Assert.Equal("CardCode", Assert.Single(draft.Header.Values).Field.Name);
        var line = Assert.Single(draft.Rows(Invoices.Child("DocumentLines")));
        Assert.Null(line.Get("LineTotal"));
        Assert.Null(line.Get("LineNum"));
    }

    [Theory]
    [InlineData("""{"CardCode":"c1","DocumentLines":[{"Quantity":1}]}""", "ItemCode")]
    [InlineData("""{"CardCode":"c1","DocumentLines":[{"ItemCode":"i1"}]}""", "Quantity")]
    public void Required_line_field_is_rejected_by_name(string body, string field)
    {
        var error = Assert.Throws<ServiceLayerException>(() => EntityBodyReader.ReadForCreate(Invoices, Json(body)));

        Assert.Contains($"'{field}'", error.Message);
        Assert.Contains("'DocumentLine'", error.Message);
    }

    [Fact]
    public void Unknown_line_field_is_rejected()
    {
        var error = Assert.Throws<ServiceLayerException>(() => EntityBodyReader.ReadForCreate(
            Invoices,
            Json("""{"CardCode":"c1","DocumentLines":[{"ItemCode":"i1","Quantity":1,"Foo":1}]}""")));

        Assert.Contains("'Foo'", error.Message);
    }

    [Fact]
    public void Lines_must_be_an_array()
    {
        Assert.Throws<ServiceLayerException>(() =>
            EntityBodyReader.ReadForCreate(Invoices, Json("""{"CardCode":"c1","DocumentLines":{}}""")));
    }

    [Fact]
    public void Purchase_invoices_have_no_installments_collection()
    {
        var error = Assert.Throws<ServiceLayerException>(() => EntityBodyReader.ReadForCreate(
            EntityCatalog.PurchaseInvoices,
            Json("""{"CardCode":"s1","DocumentInstallments":[]}""")));

        Assert.Contains("DocumentInstallments", error.Message);
    }

    [Fact]
    public void Stock_cannot_be_written_directly_on_an_item()
    {
        var draft = EntityBodyReader.ReadForCreate(EntityCatalog.Items, Json("""
            {"ItemCode":"i1","QuantityOnStock":999,"ItemWarehouseInfoCollection":[{"WarehouseCode":"01","InStock":999}]}
            """));

        Assert.Equal("ItemCode", Assert.Single(draft.Header.Values).Field.Name);
        Assert.Empty(draft.ChildrenWithRows);
    }

    [Fact]
    public void On_update_a_document_only_changes_what_is_not_fixed_at_creation()
    {
        var changes = EntityBodyReader.ReadForUpdate(Invoices, Json("""
            {"CardCode":"other","DocDate":"2026-01-01","DocTotal":1,"DocumentLines":[],"Comments":"note"}
            """));

        Assert.Equal("Comments", Assert.Single(changes.Values).Field.Name);
    }

    [Fact]
    public void Payment_row_points_to_the_invoice_through_its_own_doc_entry()
    {
        var entity = EntityCatalog.IncomingPayments;
        var draft = EntityBodyReader.ReadForCreate(entity, Json("""
            {"CardCode":"c1","CashSum":100,"PaymentInvoices":[{"DocEntry":5,"SumApplied":100,"InvoiceType":"it_Invoice"}]}
            """));

        var row = Assert.Single(draft.Rows(entity.Child("PaymentInvoices")));

        Assert.Equal(5, row.Int("DocEntry"));
        Assert.Equal("13", row.Text("InvType"));
    }
}
