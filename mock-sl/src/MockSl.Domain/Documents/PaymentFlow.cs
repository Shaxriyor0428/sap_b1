using MockSl.Domain.Metadata;

namespace MockSl.Domain.Documents;

/// <summary>
/// What makes an incoming payment different from an outgoing one.
/// A customer pays us: their debt falls and the cash account grows. We pay a supplier: the opposite.
/// </summary>
/// <param name="Invoices">The documents this payment can settle.</param>
/// <param name="PartnerCardType">The partner type, as stored in OCRD.CardType.</param>
/// <param name="DocType">ORCT.DocType or OVPM.DocType.</param>
/// <param name="InvoiceType">RCT2.InvType or VPM2.InvType: 13 is an A/R invoice, 18 an A/P invoice.</param>
/// <param name="BalanceSign">How the payment moves OCRD.Balance.</param>
/// <param name="CashSign">How the payment moves the cash account balance.</param>
public sealed record PaymentFlow(
    EntityDefinition Invoices,
    string PartnerCardType,
    string DocType,
    string InvoiceType,
    int BalanceSign,
    int CashSign)
{
    public static readonly PaymentFlow Incoming = new(EntityCatalog.Invoices, "C", "C", "13", BalanceSign: -1, CashSign: +1);

    public static readonly PaymentFlow Outgoing = new(EntityCatalog.PurchaseInvoices, "S", "S", "18", BalanceSign: +1, CashSign: -1);
}
