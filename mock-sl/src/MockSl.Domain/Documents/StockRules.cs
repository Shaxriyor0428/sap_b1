using MockSl.Domain.Errors;

namespace MockSl.Domain.Documents;

public static class StockRules
{
    /// <summary>Used when a line names no warehouse. "01" is the warehouse in the guide's samples (p.31).</summary>
    public const string DefaultWarehouse = "01";

    /// <summary>
    /// Returns the new quantity after a document moves stock, refusing to go below zero.
    /// Real SAP can be configured to allow negative stock (open question 7 in docs/sl-contract.md).
    /// </summary>
    public static decimal Apply(string itemCode, string warehouse, decimal onHand, decimal change)
    {
        var result = onHand + change;
        return result < 0 ? throw SapErrors.NotEnoughStock(itemCode, warehouse) : result;
    }
}
