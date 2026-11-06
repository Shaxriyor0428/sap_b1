using MockSl.Domain.Errors;

namespace MockSl.Domain.Documents;

/// <summary>
/// Document totals. The client never sets them: DocTotal comes from the lines.
/// Tax and discounts are not built, so a line total is simply quantity times price.
/// </summary>
public static class DocumentMath
{
    public static decimal LineTotal(decimal quantity, decimal price)
    {
        if (quantity <= 0)
        {
            throw SapErrors.AmountMustBePositive("Quantity");
        }

        if (price < 0)
        {
            throw SapErrors.AmountMustBePositive("Price");
        }

        return RoundMoney(quantity * price);
    }

    public static decimal DocumentTotal(IEnumerable<decimal> lineTotals) => RoundMoney(lineTotals.Sum());

    public static decimal RoundMoney(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
