namespace GroceryStore.UnitTests;

/// <summary>Each test is one requirement the roadmap sets for the shop's data.</summary>
public class ScenarioTests
{
    private static readonly IEnumerable<ExtraSale> AllExtras =
        Scenario.ExtraSales.Append(Scenario.CancelledSale).Append(Scenario.SaleWithCancelledPayment);

    [Fact]
    public void There_are_ten_cycles()
    {
        Assert.Equal(10, Scenario.Cycles.Count);
        Assert.Equal(Enumerable.Range(1, 10), Scenario.Cycles.Select(cycle => cycle.Number));
    }

    [Fact]
    public void Some_totals_are_above_500_and_some_below()
    {
        var totals = Scenario.Cycles.Select(cycle => Scenario.Total(cycle.Lines)).ToList();

        Assert.Contains(totals, total => total > 500);
        Assert.Contains(totals, total => total < 500);
    }

    [Fact]
    public void Dates_are_spread_over_two_months()
    {
        Assert.Contains(Scenario.Cycles, cycle => cycle.Date.IsCurrentMonth);
        Assert.Contains(Scenario.Cycles, cycle => !cycle.Date.IsCurrentMonth);
    }

    [Fact]
    public void Every_second_cycle_pays_in_part()
    {
        Assert.All(Scenario.Cycles, cycle => Assert.Equal(cycle.Number % 2 != 0, cycle.PayInFull));
    }

    [Fact]
    public void At_least_three_sales_have_installments_that_add_up_to_everything()
    {
        var withInstallments = Scenario.Cycles.Where(cycle => cycle.InstallmentPercents.Count > 0).ToList();

        Assert.True(withInstallments.Count >= 3);
        Assert.All(withInstallments, cycle => Assert.Equal(100m, cycle.InstallmentPercents.Sum()));
    }

    [Fact]
    public void Some_sales_have_two_or_three_lines()
    {
        Assert.Contains(Scenario.Cycles, cycle => cycle.Lines.Count == 2);
        Assert.Contains(Scenario.Cycles, cycle => cycle.Lines.Count == 3);
        Assert.All(Scenario.Cycles, cycle => Assert.InRange(cycle.Lines.Count, 1, 3));
    }

    [Fact]
    public void Stock_never_goes_below_zero_in_the_order_things_happen()
    {
        var stock = new Dictionary<int, decimal>();

        foreach (var cycle in Scenario.Cycles)
        {
            stock[cycle.Number] = Scenario.Products[cycle.Number - 1].PurchaseQuantity;
            SellAndCheck(stock, cycle.Lines);
        }

        foreach (var extra in AllExtras)
        {
            SellAndCheck(stock, extra.Lines);
        }
    }

    [Fact]
    public void Some_customers_buy_more_than_once_so_top_lists_mean_something()
    {
        var saleCounts = AllExtras.GroupBy(extra => extra.Customer).Select(group => group.Count() + 1);

        Assert.Contains(saleCounts, count => count > 2);
    }

    [Fact]
    public void Customers_are_distinct_because_ten_of_them_get_a_region_later()
    {
        var names = Scenario.Cycles.Select(cycle => cycle.CustomerName).Append(Scenario.CustomerWithoutSales).ToList();

        Assert.Equal(11, names.Distinct().Count());
    }

    [Fact]
    public void Extra_sales_refer_to_real_cycles()
    {
        Assert.All(AllExtras, extra => Assert.InRange(extra.Customer, 1, Scenario.Cycles.Count));
    }

    [Fact]
    public void One_cycle_has_one_product_of_its_own()
    {
        Assert.Equal(Scenario.Cycles.Count, Scenario.Products.Count);
        Assert.All(Scenario.Products, product => Assert.True(product.Price > product.Cost));
    }

    private static void SellAndCheck(Dictionary<int, decimal> stock, IEnumerable<SaleLine> lines)
    {
        foreach (var line in lines)
        {
            Assert.True(stock.ContainsKey(line.Product), $"Product {line.Product} is sold before it is bought.");
            stock[line.Product] -= line.Quantity;
            Assert.True(stock[line.Product] >= 0, $"Product {line.Product} goes below zero.");
        }
    }
}
