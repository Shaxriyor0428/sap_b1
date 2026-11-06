namespace GroceryStore;

/// <summary>
/// The roadmap's acceptance rule: after the cycles, stock equals purchases minus sales,
/// and each customer's debt equals sales minus payments. Real values are read back from the API.
/// </summary>
public sealed class Verifier(ServiceLayerConnection serviceLayer, TextWriter output)
{
    private const decimal Tolerance = 0.01m;
    private const string Closed = "bost_Close";
    private const string Open = "bost_Open";

    private int _failures;

    public async Task<bool> VerifyAsync(Ledger ledger, CancellationToken cancellationToken)
    {
        output.WriteLine();
        output.WriteLine("Stock = purchases - sales");
        foreach (var (itemCode, expected) in ledger.ExpectedStock.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            var item = await serviceLayer.GetAsync($"Items('{itemCode}')?$select=ItemCode,QuantityOnStock", cancellationToken);
            Compare(itemCode, expected, item.GetProperty("QuantityOnStock").GetDecimal());
        }

        output.WriteLine();
        output.WriteLine("Debt = sales - payments");
        foreach (var (cardCode, expected) in ledger.ExpectedDebt.OrderBy(customer => customer.Key, StringComparer.Ordinal))
        {
            var partner = await serviceLayer.GetAsync($"BusinessPartners('{cardCode}')?$select=CardCode,CurrentAccountBalance", cancellationToken);
            Compare(cardCode, expected, partner.GetProperty("CurrentAccountBalance").GetDecimal());
        }

        output.WriteLine();
        output.WriteLine("Sale status: paid in full is closed, the rest stay open");
        foreach (var sale in ledger.Sales.Where(sale => !sale.IsCancelled).OrderBy(sale => sale.DocEntry))
        {
            var invoice = await serviceLayer.GetAsync($"Invoices({sale.DocEntry})?$select=DocEntry,DocumentStatus,PaidToDate", cancellationToken);
            CompareSale(sale, invoice.GetProperty("DocumentStatus").GetString()!, invoice.GetProperty("PaidToDate").GetDecimal());
        }

        output.WriteLine();
        output.WriteLine(_failures == 0 ? "ACCEPTED: every value matches." : $"FAILED: {_failures} value(s) differ.");
        return _failures == 0;
    }

    private void Compare(string name, decimal expected, decimal actual)
    {
        var isMatch = Math.Abs(expected - actual) <= Tolerance;
        _failures += isMatch ? 0 : 1;
        output.WriteLine($"  {(isMatch ? "ok  " : "DIFF")} {name,-8} expected {expected,10:0.00}  actual {actual,10:0.00}");
    }

    private void CompareSale(SaleRecord sale, string status, decimal paidToDate)
    {
        var expectedStatus = sale.Paid >= sale.Total ? Closed : Open;
        var isMatch = status == expectedStatus && Math.Abs(sale.Paid - paidToDate) <= Tolerance;
        _failures += isMatch ? 0 : 1;
        output.WriteLine($"  {(isMatch ? "ok  " : "DIFF")} sale {sale.DocEntry,-3} total {sale.Total,9:0.00}  paid {paidToDate,9:0.00}  {status}");
    }
}
