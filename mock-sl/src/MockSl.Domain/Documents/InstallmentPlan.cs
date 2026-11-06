using MockSl.Domain.Errors;

namespace MockSl.Domain.Documents;

public sealed record InstallmentInput(DateOnly? DueDate, decimal? Total, decimal? Percentage);

public sealed record Installment(DateOnly DueDate, decimal Total, decimal Percentage);

/// <summary>
/// Splits a document total into amounts due on different dates (table INV6).
/// A document always has at least one installment: the whole total on the due date.
/// </summary>
public static class InstallmentPlan
{
    public static IReadOnlyList<Installment> Build(
        decimal documentTotal,
        DateOnly documentDueDate,
        IReadOnlyList<InstallmentInput> inputs)
    {
        if (inputs.Count == 0)
        {
            return [new Installment(documentDueDate, documentTotal, 100m)];
        }

        var plan = inputs.Select(input => ToInstallment(input, documentTotal)).ToList();
        var difference = documentTotal - plan.Sum(installment => installment.Total);

        if (difference == 0)
        {
            return plan;
        }

        // 33.33% three times misses the total by a cent, so the last one takes the remainder.
        // Amounts given directly are the client's own numbers and must add up exactly.
        var isOnlyRounding = inputs.All(input => input.Total is null) && Math.Abs(difference) < 1m;
        if (!isOnlyRounding)
        {
            throw SapErrors.InstallmentsDoNotMatchTotal();
        }

        plan[^1] = plan[^1] with { Total = plan[^1].Total + difference };
        return plan;
    }

    private static Installment ToInstallment(InstallmentInput input, decimal documentTotal)
    {
        var dueDate = input.DueDate ?? throw SapErrors.InstallmentDueDateMissing();

        if (input.Total is { } total)
        {
            return new Installment(dueDate, total, Percentage(total, documentTotal));
        }

        if (input.Percentage is { } percentage)
        {
            return new Installment(dueDate, DocumentMath.RoundMoney(documentTotal * percentage / 100m), percentage);
        }

        throw SapErrors.InstallmentAmountMissing();
    }

    private static decimal Percentage(decimal part, decimal whole) =>
        whole == 0 ? 0 : Math.Round(part / whole * 100m, 4, MidpointRounding.AwayFromZero);
}
