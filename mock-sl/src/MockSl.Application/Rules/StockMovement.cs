using MockSl.Application.Entities;
using MockSl.Domain.Documents;
using MockSl.Domain.Errors;
using MockSl.Domain.Metadata;

namespace MockSl.Application.Rules;

public sealed record StockLine(string ItemCode, string Warehouse, decimal Quantity);

/// <summary>Moves stock for the lines of a document, in OITW per warehouse and in OITM as the total.</summary>
internal static class StockMovement
{
    private static readonly EntityDefinition Items = EntityCatalog.Items;
    private static readonly ChildDefinition Warehouses = Items.Child("ItemWarehouseInfoCollection");

    public static async Task ApplyAsync(
        IEntitySession session,
        IEnumerable<StockLine> lines,
        int sign,
        CancellationToken cancellationToken)
    {
        // Two lines of one item must be checked as one amount. The fixed order means
        // two documents always lock items in the same sequence, so they cannot deadlock.
        var moves = lines
            .GroupBy(line => (line.ItemCode, line.Warehouse))
            .OrderBy(group => group.Key.ItemCode, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Warehouse, StringComparer.Ordinal);

        foreach (var move in moves)
        {
            var change = sign * move.Sum(line => line.Quantity);
            await MoveAsync(session, move.Key.ItemCode, move.Key.Warehouse, change, cancellationToken);
        }
    }

    private static async Task MoveAsync(
        IEntitySession session,
        string itemCode,
        string warehouse,
        decimal change,
        CancellationToken cancellationToken)
    {
        var key = EntityKey.Of(Items, itemCode);
        var item = await session.FindForUpdateAsync(Items, key, cancellationToken)
            ?? throw SapErrors.ReferenceNotFound("ItemCode", itemCode);

        // A service or other non-stock item appears on documents without moving stock.
        if (item.Text("InvntItem") != DocumentLifecycle.Yes)
        {
            return;
        }

        var rows = await session.QueryChildrenAsync(Warehouses, [itemCode], cancellationToken);
        var row = rows.FirstOrDefault(candidate => candidate.Text("WhsCode") == warehouse);

        var onHand = StockRules.Apply(itemCode, warehouse, row?.Decimal("OnHand") ?? 0, change);
        var stock = new FieldValue(Warehouses.Field("InStock"), onHand);

        if (row is null)
        {
            await session.InsertChildAsync(
                Warehouses,
                itemCode,
                [new FieldValue(Warehouses.Field("WarehouseCode"), warehouse), stock],
                cancellationToken);
        }
        else
        {
            await session.UpdateChildAsync(Warehouses, itemCode, warehouse, [stock], cancellationToken);
        }

        await session.AddToNumberAsync(Items, key, Items.Field("QuantityOnStock"), change, cancellationToken);
    }
}
