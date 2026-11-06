namespace GroceryStore;

/// <summary>A product of the shop. Number is its place in the list, also used in its code.</summary>
public sealed record Product(int Number, string Name, decimal PurchaseQuantity, decimal Cost, decimal Price);

public sealed record SaleLine(int Product, decimal Quantity);

/// <summary>A day in one of the two months the data is spread over.</summary>
public sealed record ScenarioDate(bool IsCurrentMonth, int Day);

/// <summary>
/// One round of the shop from the roadmap: account, supplier, product, purchase,
/// employee, customer, sale, payment.
/// </summary>
/// <param name="InstallmentPercents">Empty means one payment on the due date.</param>
/// <param name="PayInFull">False leaves part of the sale unpaid, so the customer owes money.</param>
public sealed record Cycle(
    int Number,
    ScenarioDate Date,
    string CustomerName,
    IReadOnlyList<SaleLine> Lines,
    IReadOnlyList<decimal> InstallmentPercents,
    bool PayInFull);

/// <summary>A further sale to a customer from an earlier cycle.</summary>
public sealed record ExtraSale(int Customer, ScenarioDate Date, IReadOnlyList<SaleLine> Lines, bool Pay);

/// <summary>
/// All data of the shop in one place. It is fixed, so the expected results in the sql folder stay valid.
/// The checks of the roadmap's requirements are in tools/GroceryStore.UnitTests.
/// </summary>
public static class Scenario
{
    public const decimal PartPaymentShare = 0.6m;

    /// <summary>The customer with no sales at all, which SQL task 13 looks for.</summary>
    public const string CustomerWithoutSales = "Laziz Qodirov";

    public static readonly IReadOnlyList<Product> Products =
    [
        new(1, "Issiq non", PurchaseQuantity: 200, Cost: 5, Price: 8),
        new(2, "Sut", PurchaseQuantity: 100, Cost: 10, Price: 14),
        new(3, "Guruch", PurchaseQuantity: 100, Cost: 20, Price: 28),
        new(4, "Go'sht", PurchaseQuantity: 50, Cost: 60, Price: 85),
        new(5, "Shakar", PurchaseQuantity: 100, Cost: 12, Price: 16),
        new(6, "Choy", PurchaseQuantity: 60, Cost: 25, Price: 35),
        new(7, "O'simlik yog'i", PurchaseQuantity: 60, Cost: 30, Price: 42),
        new(8, "Tuxum", PurchaseQuantity: 300, Cost: 2, Price: 3),
        new(9, "Olma", PurchaseQuantity: 120, Cost: 9, Price: 13),
        new(10, "Qatiq", PurchaseQuantity: 80, Cost: 8, Price: 11),
    ];

    public static readonly IReadOnlyList<Cycle> Cycles =
    [
        new(1, new(false, 3), "Ali Valiyev", [new(1, 100)], [], PayInFull: true),
        new(2, new(false, 8), "Bobur Toshev", [new(2, 10)], [], PayInFull: false),
        new(3, new(false, 12), "Dilshod Ergashev", [new(3, 20), new(1, 10)], [34, 33, 33], PayInFull: true),
        new(4, new(false, 18), "Eldor Rasulov", [new(4, 3)], [], PayInFull: false),
        new(5, new(false, 24), "Farrux Rahmonov", [new(5, 40)], [], PayInFull: true),
        new(6, new(true, 2), "Gulnora Saidova", [new(6, 20), new(2, 10), new(4, 2)], [50, 50], PayInFull: false),
        new(7, new(true, 6), "Hamid Ismoilov", [new(7, 5)], [], PayInFull: true),
        new(8, new(true, 10), "Ibrohim Nazarov", [new(8, 50)], [], PayInFull: false),
        new(9, new(true, 14), "Jasur Umarov", [new(9, 50)], [40, 30, 30], PayInFull: true),
        new(10, new(true, 18), "Komil Sobirov", [new(10, 20), new(5, 5)], [], PayInFull: false),
    ];

    /// <summary>Without these, every customer would have exactly one sale and the top-10 tasks would mean nothing.</summary>
    public static readonly IReadOnlyList<ExtraSale> ExtraSales =
    [
        new(1, new(true, 20), [new(1, 20)], Pay: true),
        new(1, new(true, 21), [new(9, 10)], Pay: false),
        new(3, new(true, 22), [new(3, 10)], Pay: true),
        new(5, new(true, 23), [new(6, 5)], Pay: false),
    ];

    /// <summary>A sale that is cancelled, so the SQL tasks can show why they exclude CANCELED rows.</summary>
    public static readonly ExtraSale CancelledSale = new(7, new(true, 24), [new(7, 2)], Pay: false);

    /// <summary>A sale whose payment is cancelled: the money is owed again.</summary>
    public static readonly ExtraSale SaleWithCancelledPayment = new(2, new(true, 25), [new(2, 5)], Pay: true);

    public static decimal Total(IEnumerable<SaleLine> lines) =>
        lines.Sum(line => line.Quantity * Products[line.Product - 1].Price);
}
