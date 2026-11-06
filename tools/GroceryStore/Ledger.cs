namespace GroceryStore;

public sealed record SaleRecord(int DocEntry, string CustomerCode, decimal Total, decimal Paid, bool IsCancelled);

/// <summary>
/// The script's own bookkeeping: what stock and debts must be, from the documents it created.
/// Amounts come from the Service Layer's answers, so tax on real SAP does not break the comparison.
/// </summary>
public sealed class Ledger
{
    private readonly Dictionary<string, decimal> _stock = [];
    private readonly Dictionary<string, decimal> _debt = [];
    private readonly Dictionary<int, SaleRecord> _sales = [];

    public IReadOnlyDictionary<string, decimal> ExpectedStock => _stock;

    public IReadOnlyDictionary<string, decimal> ExpectedDebt => _debt;

    public IEnumerable<SaleRecord> Sales => _sales.Values;

    public void AddCustomer(string code) => _debt.TryAdd(code, 0m);

    public void AddPurchase(string itemCode, decimal quantity) =>
        _stock[itemCode] = _stock.GetValueOrDefault(itemCode) + quantity;

    public void AddSale(int docEntry, string customerCode, decimal total, IEnumerable<(string ItemCode, decimal Quantity)> lines)
    {
        foreach (var (itemCode, quantity) in lines)
        {
            _stock[itemCode] = _stock.GetValueOrDefault(itemCode) - quantity;
        }

        _debt[customerCode] = _debt.GetValueOrDefault(customerCode) + total;
        _sales[docEntry] = new SaleRecord(docEntry, customerCode, total, Paid: 0m, IsCancelled: false);
    }

    public void AddPayment(int saleDocEntry, decimal amount)
    {
        var sale = _sales[saleDocEntry];
        _debt[sale.CustomerCode] -= amount;
        _sales[saleDocEntry] = sale with { Paid = sale.Paid + amount };
    }

    public void CancelPayment(int saleDocEntry, decimal amount) => AddPayment(saleDocEntry, -amount);

    public void CancelSale(int docEntry, IEnumerable<(string ItemCode, decimal Quantity)> lines)
    {
        foreach (var (itemCode, quantity) in lines)
        {
            _stock[itemCode] += quantity;
        }

        var sale = _sales[docEntry];
        _debt[sale.CustomerCode] -= sale.Total;
        _sales[docEntry] = sale with { IsCancelled = true };
    }
}
