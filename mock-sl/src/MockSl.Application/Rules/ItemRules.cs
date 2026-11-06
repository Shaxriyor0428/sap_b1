using MockSl.Application.Entities;
using MockSl.Domain.Documents;
using MockSl.Domain.Metadata;

namespace MockSl.Application.Rules;

/// <summary>A new item starts with no stock in the default warehouse. After that, only documents move stock.</summary>
public sealed class ItemRules : EntityRules
{
    private static readonly ChildDefinition Warehouses = EntityCatalog.Items.Child("ItemWarehouseInfoCollection");

    public override Task AfterInsertAsync(
        EntityDraft draft,
        EntityRow row,
        IEntitySession session,
        CancellationToken cancellationToken) =>
        session.InsertChildAsync(
            Warehouses,
            row["ItemCode"],
            [new FieldValue(Warehouses.Field("WarehouseCode"), StockRules.DefaultWarehouse)],
            cancellationToken);
}
