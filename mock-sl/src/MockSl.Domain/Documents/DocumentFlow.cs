namespace MockSl.Domain.Documents;

/// <summary>
/// What makes a sale different from a purchase. The steps are the same, only the direction changes:
/// a sale takes goods out and the customer owes us; a purchase brings goods in and we owe the supplier.
/// </summary>
/// <param name="PartnerCardType">The partner type this document needs, as stored in OCRD.CardType.</param>
/// <param name="StockSign">+1 adds to stock, -1 takes from it.</param>
/// <param name="BalanceSign">How the document total moves OCRD.Balance.</param>
public sealed record DocumentFlow(string PartnerCardType, int StockSign, int BalanceSign)
{
    public static readonly DocumentFlow Sale = new("C", StockSign: -1, BalanceSign: +1);

    public static readonly DocumentFlow Purchase = new("S", StockSign: +1, BalanceSign: -1);
}
