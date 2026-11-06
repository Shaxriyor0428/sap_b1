using MockSl.Application.Entities;
using MockSl.Domain.Metadata;

namespace MockSl.UnitTests.Entities;

public class EntitySerializerTests
{
    private static EntityRow Row(params (string Column, object? Value)[] columns) =>
        new(columns.ToDictionary(column => column.Column, column => column.Value));

    [Fact]
    public void Row_uses_service_layer_names_and_enum_names()
    {
        var entity = EntityCatalog.BusinessPartners;
        var fields = new[] { "CardType", "EmailAddress", "Phone1", "CurrentAccountBalance" }
            .Select(name => entity.FindField(name)!);

        var json = EntitySerializer.ToJson(fields, Row(
            ("CardType", "C"),
            ("E_Mail", "a@b.uz"),
            ("Phone1", null),
            ("Balance", 1500.500000m)));

        Assert.Equal(
            """{"CardType":"cCustomer","EmailAddress":"a@b.uz","Phone1":null,"CurrentAccountBalance":1500.5}""",
            json.ToJsonString());
    }

    [Fact]
    public void Document_header_shows_status_and_dates_the_service_layer_way()
    {
        var entity = EntityCatalog.Invoices;
        var fields = new[] { "DocumentStatus", "Cancelled", "CancelStatus", "DocDate", "DocumentsOwner" }
            .Select(name => entity.FindField(name)!);

        var json = EntitySerializer.ToJson(fields, Row(
            ("DocStatus", "O"),
            ("CANCELED", "N"),
            ("DocDate", new DateTime(2026, 11, 1)),
            ("OwnerCode", 7)));

        Assert.Equal(
            """{"DocumentStatus":"bost_Open","Cancelled":"tNO","CancelStatus":"csNo","DocDate":"2026-11-01T00:00:00Z","DocumentsOwner":7}""",
            json.ToJsonString());
    }

    [Fact]
    public void Collection_rows_become_a_json_array()
    {
        var child = EntityCatalog.Items.Child("ItemWarehouseInfoCollection");

        var json = EntitySerializer.ToJsonArray(child, [Row(("ItemCode", "i1"), ("WhsCode", "01"), ("OnHand", 5m))]);

        Assert.Equal("""[{"ItemCode":"i1","WarehouseCode":"01","InStock":5}]""", json.ToJsonString());
    }

    [Fact]
    public void Payment_rows_keep_the_names_of_the_api()
    {
        var child = EntityCatalog.IncomingPayments.Child("PaymentInvoices");

        var json = EntitySerializer.ToJsonArray(child, [Row(
            ("DocNum", 3),
            ("InvoiceId", 0),
            ("DocEntry", 5),
            ("SumApplied", 100m),
            ("InvType", "13"),
            ("InstId", 1))]);

        Assert.Equal(
            """[{"LineNum":0,"DocEntry":5,"SumApplied":100,"InvoiceType":"it_Invoice","InstallmentId":1}]""",
            json.ToJsonString());
    }
}
