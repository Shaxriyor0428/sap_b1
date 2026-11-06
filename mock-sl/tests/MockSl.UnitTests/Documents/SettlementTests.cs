using MockSl.Domain.Documents;
using MockSl.Domain.Errors;

namespace MockSl.UnitTests.Documents;

public class SettlementTests
{
    private static InvoiceState Invoice(decimal paid = 0m, string canceled = "N", string status = "O") =>
        new(DocEntry: 5, CardCode: "c1", canceled, status, DocTotal: 300m, PaidToDate: paid);

    [Fact]
    public void Part_payment_raises_paid_to_date_and_leaves_the_document_open()
    {
        var change = Settlement.Apply(Invoice(), "c1", 100m);

        Assert.Equal(new InvoiceChange(100m, "O"), change);
    }

    [Fact]
    public void Full_payment_closes_the_document()
    {
        var change = Settlement.Apply(Invoice(paid: 100m), "c1", 200m);

        Assert.Equal(new InvoiceChange(300m, "C"), change);
    }

    [Fact]
    public void Paying_more_than_is_open_is_rejected()
    {
        var error = Assert.Throws<ServiceLayerException>(() => Settlement.Apply(Invoice(paid: 100m), "c1", 200.01m));

        Assert.Contains("5", error.Message);
    }

    [Fact]
    public void Paying_another_partners_document_is_rejected()
    {
        Assert.Throws<ServiceLayerException>(() => Settlement.Apply(Invoice(), "c2", 100m));
    }

    [Theory]
    [InlineData("Y", "C")]
    [InlineData("N", "C")]
    public void Cancelled_or_closed_documents_cannot_be_paid(string canceled, string status)
    {
        Assert.Throws<ServiceLayerException>(() => Settlement.Apply(Invoice(canceled: canceled, status: status), "c1", 1m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Applied_sum_must_be_positive(decimal applied)
    {
        Assert.Throws<ServiceLayerException>(() => Settlement.Apply(Invoice(), "c1", applied));
    }

    [Fact]
    public void Cancelling_a_payment_opens_the_document_again()
    {
        var change = Settlement.Reverse(Invoice(paid: 300m, status: "C"), 100m);

        Assert.Equal(new InvoiceChange(200m, "O"), change);
    }

    private static IReadOnlyList<InstallmentState> Installments(decimal firstPaid = 0m, decimal secondPaid = 0m) =>
        [new(1, 100m, firstPaid), new(2, 200m, secondPaid)];

    [Fact]
    public void Payment_goes_to_the_earliest_installment_first()
    {
        var changes = Settlement.AllocateToInstallments(Installments(), 150m, requestedId: null);

        Assert.Equal([new InstallmentChange(1, 100m, "C"), new InstallmentChange(2, 50m, "O")], changes);
    }

    [Fact]
    public void Installment_already_paid_is_skipped()
    {
        var change = Assert.Single(Settlement.AllocateToInstallments(Installments(firstPaid: 100m), 50m, requestedId: null));

        Assert.Equal(new InstallmentChange(2, 50m, "O"), change);
    }

    [Fact]
    public void Client_can_name_the_installment()
    {
        var change = Assert.Single(Settlement.AllocateToInstallments(Installments(), 200m, requestedId: 2));

        Assert.Equal(new InstallmentChange(2, 200m, "C"), change);
    }

    [Fact]
    public void Paying_more_than_the_named_installment_is_rejected()
    {
        Assert.Throws<ServiceLayerException>(() => Settlement.AllocateToInstallments(Installments(), 150m, requestedId: 1));
    }

    [Fact]
    public void Cancelled_payment_comes_back_from_the_latest_installment_first()
    {
        var changes = Settlement.ReverseFromInstallments(Installments(firstPaid: 100m, secondPaid: 50m), 120m);

        Assert.Equal([new InstallmentChange(2, 0m, "O"), new InstallmentChange(1, 30m, "O")], changes);
    }

    [Fact]
    public void Allocation_and_reversal_cancel_each_other_out()
    {
        var after = Settlement.AllocateToInstallments(Installments(), 150m, requestedId: null);
        var state = Installments(after[0].PaidToDate, after[1].PaidToDate);

        var reversed = Settlement.ReverseFromInstallments(state, 150m);

        Assert.All(reversed, change => Assert.Equal(0m, change.PaidToDate));
    }

    [Fact]
    public void Incoming_payment_lowers_the_debt_and_raises_the_cash()
    {
        Assert.Equal(-1, PaymentFlow.Incoming.BalanceSign);
        Assert.Equal(+1, PaymentFlow.Incoming.CashSign);
        Assert.Equal("13", PaymentFlow.Incoming.InvoiceType);
    }

    [Fact]
    public void Outgoing_payment_works_the_other_way()
    {
        Assert.Equal(+1, PaymentFlow.Outgoing.BalanceSign);
        Assert.Equal(-1, PaymentFlow.Outgoing.CashSign);
        Assert.Equal("18", PaymentFlow.Outgoing.InvoiceType);
    }
}
