using MockSl.Domain.Errors;

namespace MockSl.Domain.Documents;

public sealed record InvoiceState(int DocEntry, string CardCode, string Canceled, string Status, decimal DocTotal, decimal PaidToDate);

public sealed record InvoiceChange(decimal PaidToDate, string Status);

public sealed record InstallmentState(int Id, decimal Total, decimal PaidToDate);

public sealed record InstallmentChange(int Id, decimal PaidToDate, string Status);

/// <summary>
/// How a payment changes a document. Paid in full: the document closes (bost_Close).
/// Paid in part: it stays open and the rest is still owed.
/// </summary>
public static class Settlement
{
    public static InvoiceChange Apply(InvoiceState invoice, string paymentCardCode, decimal applied)
    {
        if (applied <= 0)
        {
            throw SapErrors.AmountMustBePositive("SumApplied");
        }

        if (invoice.CardCode != paymentCardCode)
        {
            throw SapErrors.InvoiceOfAnotherPartner(invoice.DocEntry);
        }

        if (invoice.Canceled != DocumentLifecycle.No || invoice.Status != DocumentLifecycle.Open)
        {
            throw SapErrors.InvoiceNotOpen(invoice.DocEntry);
        }

        var paid = invoice.PaidToDate + applied;
        if (paid > invoice.DocTotal)
        {
            throw SapErrors.PaymentExceedsOpenAmount(invoice.DocEntry);
        }

        return new InvoiceChange(paid, paid == invoice.DocTotal ? DocumentLifecycle.Closed : DocumentLifecycle.Open);
    }

    /// <summary>A cancelled payment makes the money owed again, so a document it had closed opens again.</summary>
    public static InvoiceChange Reverse(InvoiceState invoice, decimal applied)
    {
        var paid = Math.Max(invoice.PaidToDate - applied, 0);
        var stillPaidInFull = paid >= invoice.DocTotal;
        return new InvoiceChange(paid, stillPaidInFull ? DocumentLifecycle.Closed : DocumentLifecycle.Open);
    }

    /// <summary>
    /// Spreads a payment over installments, earliest first, unless the client names one.
    /// Returns only the installments that change.
    /// </summary>
    public static IReadOnlyList<InstallmentChange> AllocateToInstallments(
        IReadOnlyList<InstallmentState> installments,
        decimal applied,
        int? requestedId)
    {
        var changes = new List<InstallmentChange>();
        var remaining = applied;

        foreach (var installment in installments.OrderBy(installment => installment.Id))
        {
            if (remaining == 0)
            {
                break;
            }

            if (requestedId is not null && installment.Id != requestedId)
            {
                continue;
            }

            var open = installment.Total - installment.PaidToDate;
            var part = Math.Min(open, remaining);
            if (part == 0)
            {
                continue;
            }

            remaining -= part;
            var paid = installment.PaidToDate + part;
            changes.Add(new InstallmentChange(installment.Id, paid, paid == installment.Total ? DocumentLifecycle.Closed : DocumentLifecycle.Open));
        }

        if (remaining != 0 && requestedId is { } id)
        {
            throw SapErrors.PaymentExceedsInstallment(id);
        }

        return changes;
    }

    /// <summary>Takes a cancelled payment back, latest installment first, the mirror of the allocation above.</summary>
    public static IReadOnlyList<InstallmentChange> ReverseFromInstallments(
        IReadOnlyList<InstallmentState> installments,
        decimal applied)
    {
        var changes = new List<InstallmentChange>();
        var remaining = applied;

        foreach (var installment in installments.OrderByDescending(installment => installment.Id))
        {
            if (remaining == 0)
            {
                break;
            }

            var part = Math.Min(installment.PaidToDate, remaining);
            if (part == 0)
            {
                continue;
            }

            remaining -= part;
            changes.Add(new InstallmentChange(installment.Id, installment.PaidToDate - part, DocumentLifecycle.Open));
        }

        return changes;
    }
}
