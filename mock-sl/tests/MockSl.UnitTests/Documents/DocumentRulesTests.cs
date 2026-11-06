using MockSl.Domain.Documents;
using MockSl.Domain.Errors;

namespace MockSl.UnitTests.Documents;

public class DocumentRulesTests
{
    private static readonly DateOnly DueDate = new(2026, 11, 30);

    [Theory]
    [InlineData(2, 100, 200)]
    [InlineData(1.5, 3.33, 5)] // 4.995 rounds up to money
    public void Line_total_is_quantity_times_price(decimal quantity, decimal price, decimal expected)
    {
        Assert.Equal(expected, DocumentMath.LineTotal(quantity, price));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(1, -1)]
    public void Line_needs_a_positive_quantity_and_no_negative_price(decimal quantity, decimal price)
    {
        var error = Assert.Throws<ServiceLayerException>(() => DocumentMath.LineTotal(quantity, price));

        Assert.Equal("-10", error.Code);
    }

    [Fact]
    public void Document_total_is_the_sum_of_its_lines()
    {
        Assert.Equal(350m, DocumentMath.DocumentTotal([200m, 150m]));
    }

    [Fact]
    public void A_sale_takes_stock_and_makes_the_customer_owe()
    {
        Assert.Equal(-1, DocumentFlow.Sale.StockSign);
        Assert.Equal(+1, DocumentFlow.Sale.BalanceSign);
        Assert.Equal("C", DocumentFlow.Sale.PartnerCardType);
    }

    [Fact]
    public void A_purchase_adds_stock_and_works_the_other_way()
    {
        Assert.Equal(+1, DocumentFlow.Purchase.StockSign);
        Assert.Equal(-1, DocumentFlow.Purchase.BalanceSign);
        Assert.Equal("S", DocumentFlow.Purchase.PartnerCardType);
    }

    [Fact]
    public void Without_installments_the_whole_total_is_due_on_the_due_date()
    {
        var installment = Assert.Single(InstallmentPlan.Build(300m, DueDate, []));

        Assert.Equal(new Installment(DueDate, 300m, 100m), installment);
    }

    [Fact]
    public void Installments_given_as_amounts_keep_their_dates()
    {
        var first = new DateOnly(2026, 11, 1);
        var second = new DateOnly(2026, 12, 1);

        var plan = InstallmentPlan.Build(300m, DueDate, [new InstallmentInput(first, 100m, null), new InstallmentInput(second, 200m, null)]);

        Assert.Equal(first, plan[0].DueDate);
        Assert.Equal(100m, plan[0].Total);
        Assert.Equal(200m, plan[1].Total);
    }

    [Fact]
    public void Installments_given_as_percentages_add_up_to_the_exact_total()
    {
        var inputs = Enumerable.Range(0, 3).Select(_ => new InstallmentInput(DueDate, null, 33.33m)).ToList();

        // 33.33% of 100 is 33.33, and three of them are 99.99: the last one takes the missing cent.
        var plan = InstallmentPlan.Build(100m, DueDate, inputs);

        Assert.Equal([33.33m, 33.33m, 33.34m], plan.Select(installment => installment.Total));
        Assert.Equal(100m, plan.Sum(installment => installment.Total));
    }

    [Fact]
    public void Amounts_that_do_not_add_up_are_rejected()
    {
        Assert.Throws<ServiceLayerException>(() =>
            InstallmentPlan.Build(300m, DueDate, [new InstallmentInput(DueDate, 100m, null)]));
    }

    [Fact]
    public void Percentages_far_from_the_total_are_rejected()
    {
        Assert.Throws<ServiceLayerException>(() =>
            InstallmentPlan.Build(300m, DueDate, [new InstallmentInput(DueDate, null, 50m)]));
    }

    [Fact]
    public void Installment_without_a_date_is_rejected()
    {
        Assert.Throws<ServiceLayerException>(() =>
            InstallmentPlan.Build(300m, DueDate, [new InstallmentInput(null, 300m, null)]));
    }

    [Fact]
    public void Installment_without_an_amount_is_rejected()
    {
        Assert.Throws<ServiceLayerException>(() =>
            InstallmentPlan.Build(300m, DueDate, [new InstallmentInput(DueDate, null, null)]));
    }

    [Theory]
    [InlineData(10, 5, 15)]
    [InlineData(10, -10, 0)]
    public void Stock_moves_by_the_document_quantity(decimal onHand, decimal change, decimal expected)
    {
        Assert.Equal(expected, StockRules.Apply("i1", "01", onHand, change));
    }

    [Fact]
    public void Stock_cannot_go_below_zero()
    {
        var error = Assert.Throws<ServiceLayerException>(() => StockRules.Apply("i1", "01", 10m, -10.5m));

        Assert.Contains("'i1'", error.Message);
    }

    [Fact]
    public void An_open_unpaid_document_can_be_cancelled()
    {
        DocumentLifecycle.EnsureCanCancel("N", "O", 0m);
    }

    [Theory]
    [InlineData("Y", "C", 0)] // already cancelled
    [InlineData("N", "O", 1)] // has payments
    [InlineData("N", "C", 0)] // closed
    public void Other_documents_cannot_be_cancelled(string canceled, string status, decimal paidToDate)
    {
        Assert.Throws<ServiceLayerException>(() => DocumentLifecycle.EnsureCanCancel(canceled, status, paidToDate));
    }

    [Fact]
    public void An_open_document_can_be_closed()
    {
        DocumentLifecycle.EnsureCanClose("N", "O");
    }

    [Theory]
    [InlineData("Y", "C")]
    [InlineData("N", "C")]
    public void Cancelled_or_closed_documents_cannot_be_closed(string canceled, string status)
    {
        Assert.Throws<ServiceLayerException>(() => DocumentLifecycle.EnsureCanClose(canceled, status));
    }
}
