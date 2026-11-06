using MockSl.Domain.Documents;
using MockSl.Domain.Metadata;

namespace MockSl.Application.Rules;

/// <summary>Which rules belong to which object. Objects that are not listed have no special behaviour.</summary>
public sealed class RulesCatalog(TimeProvider clock)
{
    private static readonly EntityRules NoRules = new PlainRules();

    // By name: a definition with user-defined fields is a new copy of the same object.
    private readonly Dictionary<string, EntityRules> _rules = new()
    {
        [EntityCatalog.Items.SetName] = new ItemRules(),
        [EntityCatalog.ChartOfAccounts.SetName] = new AccountRules(),
        [EntityCatalog.UserFieldsMD.SetName] = new UserFieldRules(),
        [EntityCatalog.Invoices.SetName] = new DocumentRules(EntityCatalog.Invoices, DocumentFlow.Sale, clock),
        [EntityCatalog.PurchaseInvoices.SetName] = new DocumentRules(EntityCatalog.PurchaseInvoices, DocumentFlow.Purchase, clock),
        [EntityCatalog.IncomingPayments.SetName] = new PaymentRules(EntityCatalog.IncomingPayments, PaymentFlow.Incoming, clock),
        [EntityCatalog.VendorPayments.SetName] = new PaymentRules(EntityCatalog.VendorPayments, PaymentFlow.Outgoing, clock),
    };

    public EntityRules For(EntityDefinition entity) => _rules.GetValueOrDefault(entity.SetName, NoRules);

    private sealed class PlainRules : EntityRules;
}
