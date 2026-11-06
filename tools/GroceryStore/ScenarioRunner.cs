using System.Globalization;

namespace GroceryStore;

/// <summary>Creates the shop through the Service Layer API, in the order the roadmap gives.</summary>
public sealed class ScenarioRunner(ServiceLayerConnection serviceLayer, GrocerySettings settings, TextWriter output)
{
    private readonly Ledger _ledger = new();
    private readonly List<string> _customers = [];
    private readonly List<int> _employees = [];
    private readonly List<string> _accounts = [];

    public async Task<Ledger> RunAsync(CancellationToken cancellationToken)
    {
        foreach (var cycle in Scenario.Cycles)
        {
            await RunCycleAsync(cycle, cancellationToken);
        }

        await RunExtrasAsync(cancellationToken);
        return _ledger;
    }

    private async Task RunCycleAsync(Cycle cycle, CancellationToken cancellationToken)
    {
        var number = cycle.Number;
        var date = ToDate(cycle.Date);
        var product = Scenario.Products[number - 1];

        // 1. Cash account
        var account = await serviceLayer.CreateAsync(
            "ChartOfAccounts",
            new
            {
                FormatCode = Code("K", number),
                Name = $"Kassa {number:00}",
                FatherAccountKey = settings.CashParentAccount,
                CashAccount = "tYES",
                ActiveAccount = "tYES",
            },
            cancellationToken);
        var accountCode = account.GetProperty("Code").GetString()!;
        _accounts.Add(accountCode);

        // 2. Supplier
        var supplierCode = Code("S", number);
        await serviceLayer.CreateAsync(
            "BusinessPartners",
            new { CardCode = supplierCode, CardName = $"Ta'minotchi {number:00}", CardType = "cSupplier", Phone1 = Phone(71, number) },
            cancellationToken);

        // 3. Product
        var itemCode = ItemCode(product.Number);
        await serviceLayer.CreateAsync(
            "Items",
            new { ItemCode = itemCode, ItemName = product.Name, InventoryItem = "tYES", SalesItem = "tYES", PurchaseItem = "tYES" },
            cancellationToken);

        // 4. Purchase: the goods arrive, so the sale below cannot take stock below zero.
        await serviceLayer.CreateAsync(
            "PurchaseInvoices",
            new
            {
                CardCode = supplierCode,
                DocDate = Text(date),
                DocDueDate = Text(date),
                DocumentLines = new[] { Line(itemCode, product.PurchaseQuantity, product.Cost) },
            },
            cancellationToken);
        _ledger.AddPurchase(itemCode, product.PurchaseQuantity);

        // 5. Employee
        var employee = await serviceLayer.CreateAsync(
            "EmployeesInfo",
            new { FirstName = "Sotuvchi", LastName = $"{number:00}", JobTitle = "Sotuvchi" },
            cancellationToken);
        var employeeId = employee.GetProperty("EmployeeID").GetInt32();
        _employees.Add(employeeId);

        // 6. Customer
        var customerCode = await CreateCustomerAsync(cycle.CustomerName, number, cancellationToken);
        _customers.Add(customerCode);

        // 7. Sale, with the employee attached
        var sale = await CreateSaleAsync(customerCode, employeeId, date, cycle.Lines, cycle.InstallmentPercents, cancellationToken);

        // 8. Payment into this cycle's cash account, in full or in part
        var share = cycle.PayInFull ? 1m : Scenario.PartPaymentShare;
        var paid = Math.Round(sale.Total * share, 2, MidpointRounding.AwayFromZero);
        await CreatePaymentAsync(customerCode, accountCode, date.AddDays(1), sale.DocEntry, paid, cancellationToken);

        output.WriteLine($"Cycle {number,2}: {cycle.CustomerName,-18} sale {sale.Total,9:0.00}  paid {paid,9:0.00}  lines {cycle.Lines.Count}  installments {Math.Max(cycle.InstallmentPercents.Count, 1)}");
    }

    private async Task RunExtrasAsync(CancellationToken cancellationToken)
    {
        await CreateCustomerAsync(Scenario.CustomerWithoutSales, Scenario.Cycles.Count + 1, cancellationToken);
        output.WriteLine($"Extra   : {Scenario.CustomerWithoutSales} has no sales");

        foreach (var extra in Scenario.ExtraSales)
        {
            await CreateExtraSaleAsync(extra, cancellationToken);
        }

        // A cancelled sale: stock and debt must return to where they were.
        var cancelled = await CreateExtraSaleAsync(Scenario.CancelledSale, cancellationToken);
        await serviceLayer.RunActionAsync($"Invoices({cancelled.DocEntry})/Cancel", cancellationToken);
        _ledger.CancelSale(cancelled.DocEntry, ToItemLines(Scenario.CancelledSale.Lines));
        output.WriteLine($"Extra   : sale {cancelled.DocEntry} cancelled");

        // A cancelled payment: the money is owed again.
        var sale = await CreateExtraSaleAsync(Scenario.SaleWithCancelledPayment, cancellationToken);
        var customer = _customers[Scenario.SaleWithCancelledPayment.Customer - 1];
        var payments = await serviceLayer.GetAsync(
            $"IncomingPayments?$select=DocEntry,CardCode&$filter={Uri.EscapeDataString($"CardCode eq '{customer}'")}&$orderby=DocEntry desc&$top=1",
            cancellationToken);
        var paymentEntry = payments.GetProperty("value")[0].GetProperty("DocEntry").GetInt32();
        await serviceLayer.RunActionAsync($"IncomingPayments({paymentEntry})/Cancel", cancellationToken);
        _ledger.CancelPayment(sale.DocEntry, sale.Total);
        output.WriteLine($"Extra   : payment {paymentEntry} cancelled");
    }

    private async Task<(int DocEntry, decimal Total)> CreateExtraSaleAsync(ExtraSale extra, CancellationToken cancellationToken)
    {
        var date = ToDate(extra.Date);
        var customerCode = _customers[extra.Customer - 1];
        var employeeId = _employees[extra.Customer - 1];

        var sale = await CreateSaleAsync(customerCode, employeeId, date, extra.Lines, [], cancellationToken);

        if (extra.Pay)
        {
            await CreatePaymentAsync(customerCode, _accounts[extra.Customer - 1], date.AddDays(1), sale.DocEntry, sale.Total, cancellationToken);
        }

        output.WriteLine($"Extra   : sale to {customerCode,-6} {sale.Total,9:0.00}  paid {(extra.Pay ? sale.Total : 0m),9:0.00}");
        return sale;
    }

    private async Task<string> CreateCustomerAsync(string name, int number, CancellationToken cancellationToken)
    {
        var code = Code("C", number);

        await serviceLayer.CreateAsync(
            "BusinessPartners",
            new
            {
                CardCode = code,
                CardName = name,
                CardType = "cCustomer",
                Phone1 = Phone(90, number),
                Address = $"Amir Temur {number}",
                City = number % 2 == 0 ? "Samarqand" : "Toshkent",
            },
            cancellationToken);

        _ledger.AddCustomer(code);
        return code;
    }

    private async Task<(int DocEntry, decimal Total)> CreateSaleAsync(
        string customerCode,
        int employeeId,
        DateOnly date,
        IReadOnlyList<SaleLine> lines,
        IReadOnlyList<decimal> installmentPercents,
        CancellationToken cancellationToken)
    {
        var body = new Dictionary<string, object?>
        {
            ["CardCode"] = customerCode,
            ["DocDate"] = Text(date),
            ["DocDueDate"] = Text(date.AddDays(30)),

            // OINV.OwnerCode points to employees. SlpCode would point to sales employees (OSLP) instead.
            ["DocumentsOwner"] = employeeId,
            ["DocumentLines"] = lines.Select(line =>
            {
                var product = Scenario.Products[line.Product - 1];
                return Line(ItemCode(product.Number), line.Quantity, product.Price);
            }).ToList(),
        };

        if (installmentPercents.Count > 0)
        {
            // Percentages, not amounts: on real SAP the total may include tax, which only the server knows.
            body["DocumentInstallments"] = installmentPercents
                .Select((percent, index) => new { DueDate = Text(date.AddMonths(index + 1)), Percentage = percent })
                .ToList();
        }

        var sale = await serviceLayer.CreateAsync("Invoices", body, cancellationToken);
        var docEntry = sale.GetProperty("DocEntry").GetInt32();
        var total = sale.GetProperty("DocTotal").GetDecimal();

        _ledger.AddSale(docEntry, customerCode, total, ToItemLines(lines));
        return (docEntry, total);
    }

    private async Task CreatePaymentAsync(
        string customerCode,
        string accountCode,
        DateOnly date,
        int saleDocEntry,
        decimal amount,
        CancellationToken cancellationToken)
    {
        await serviceLayer.CreateAsync(
            "IncomingPayments",
            new
            {
                CardCode = customerCode,
                DocDate = Text(date),
                CashAccount = accountCode,
                CashSum = amount,
                PaymentInvoices = new[] { new { DocEntry = saleDocEntry, SumApplied = amount, InvoiceType = "it_Invoice" } },
            },
            cancellationToken);

        _ledger.AddPayment(saleDocEntry, amount);
    }

    private IEnumerable<(string ItemCode, decimal Quantity)> ToItemLines(IEnumerable<SaleLine> lines) =>
        lines.Select(line => (ItemCode(line.Product), line.Quantity));

    private Dictionary<string, object?> Line(string itemCode, decimal quantity, decimal unitPrice)
    {
        var line = new Dictionary<string, object?>
        {
            ["ItemCode"] = itemCode,
            ["Quantity"] = quantity,
            ["UnitPrice"] = unitPrice,
            ["WarehouseCode"] = settings.Warehouse,
        };

        // Which of these is needed depends on the company's localization, so it is a setting.
        if (settings.TaxCode is not null)
        {
            line["TaxCode"] = settings.TaxCode;
        }

        if (settings.VatGroup is not null)
        {
            line["VatGroup"] = settings.VatGroup;
        }

        return line;
    }

    private string ItemCode(int number) => Code("I", number);

    private string Code(string kind, int number) => $"{settings.Prefix}{kind}{number:00}";

    private static string Phone(int area, int number) => $"+998{area}{1000000 + number}";

    private DateOnly ToDate(ScenarioDate date)
    {
        var month = date.IsCurrentMonth ? settings.CurrentMonth : settings.PreviousMonth;
        return month.AddDays(date.Day - 1);
    }

    private static string Text(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
