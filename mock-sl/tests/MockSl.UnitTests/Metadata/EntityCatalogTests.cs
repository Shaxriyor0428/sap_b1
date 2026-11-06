using MockSl.Domain.Metadata;

namespace MockSl.UnitTests.Metadata;

public class EntityCatalogTests
{
    [Theory]
    [InlineData("BusinessPartners", "OCRD", "CardCode")]
    [InlineData("EmployeesInfo", "OHEM", "EmployeeID")]
    [InlineData("Items", "OITM", "ItemCode")]
    [InlineData("ChartOfAccounts", "OACT", "Code")]
    [InlineData("Invoices", "OINV", "DocEntry")]
    [InlineData("PurchaseInvoices", "OPCH", "DocEntry")]
    [InlineData("IncomingPayments", "ORCT", "DocEntry")]
    [InlineData("VendorPayments", "OVPM", "DocEntry")]
    public void Every_object_from_the_roadmap_is_available(string setName, string table, string key)
    {
        var entity = EntityCatalog.Find(setName)!;

        Assert.Equal(table, entity.Table);
        Assert.Equal(key, Assert.Single(entity.Key).Name);
    }

    [Theory]
    [InlineData("Invoices", "DocumentLines", "INV1", "DocEntry")]
    [InlineData("Invoices", "DocumentInstallments", "INV6", "DocEntry")]
    [InlineData("PurchaseInvoices", "DocumentLines", "PCH1", "DocEntry")]
    [InlineData("IncomingPayments", "PaymentInvoices", "RCT2", "DocNum")]
    [InlineData("VendorPayments", "PaymentInvoices", "VPM2", "DocNum")]
    [InlineData("Items", "ItemWarehouseInfoCollection", "OITW", "ItemCode")]
    public void Collections_live_in_their_own_tables(string setName, string child, string table, string parentKey)
    {
        var definition = EntityCatalog.Find(setName)!.Child(child);

        Assert.Equal(table, definition.Table);
        Assert.Equal(parentKey, definition.ParentKeyColumn);
    }

    [Theory]
    [InlineData("BusinessPartners", true)]
    [InlineData("Items", true)]
    [InlineData("EmployeesInfo", true)]
    [InlineData("ChartOfAccounts", true)]
    [InlineData("Invoices", false)]
    [InlineData("PurchaseInvoices", false)]
    [InlineData("IncomingPayments", false)]
    [InlineData("VendorPayments", false)]
    public void Only_master_data_can_be_deleted(string setName, bool canDelete)
    {
        Assert.Equal(canDelete, EntityCatalog.Find(setName)!.CanDelete);
    }

    [Fact]
    public void Default_accounts_are_not_an_entity_set()
    {
        Assert.Null(EntityCatalog.Find("PeriodCategories"));
    }

    [Theory]
    [InlineData("Items", "QuantityOnStock")]
    [InlineData("Invoices", "DocTotal")]
    [InlineData("Invoices", "PaidToDate")]
    [InlineData("Invoices", "DocumentStatus")]
    [InlineData("PurchaseInvoices", "DocTotal")]
    [InlineData("BusinessPartners", "CurrentAccountBalance")]
    [InlineData("ChartOfAccounts", "Balance")]
    public void Values_that_only_documents_may_change_are_read_only(string setName, string field)
    {
        Assert.True(EntityCatalog.Find(setName)!.Field(field).IsReadOnly);
    }
}
