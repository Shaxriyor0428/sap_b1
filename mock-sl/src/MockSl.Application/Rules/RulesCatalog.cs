using MockSl.Domain.Documents;
using MockSl.Domain.Metadata;

namespace MockSl.Application.Rules;

/// <summary>Which rules belong to which object. Objects that are not listed have no special behaviour.</summary>
public sealed class RulesCatalog(TimeProvider clock)
{
    private static readonly EntityRules NoRules = new PlainRules();

    private readonly Dictionary<EntityDefinition, EntityRules> _rules = new()
    {
        [EntityCatalog.Items] = new ItemRules(),
        [EntityCatalog.ChartOfAccounts] = new AccountRules(),
        [EntityCatalog.Invoices] = new DocumentRules(EntityCatalog.Invoices, DocumentFlow.Sale, clock),
        [EntityCatalog.PurchaseInvoices] = new DocumentRules(EntityCatalog.PurchaseInvoices, DocumentFlow.Purchase, clock),
        [EntityCatalog.IncomingPayments] = new PaymentRules(EntityCatalog.IncomingPayments, PaymentFlow.Incoming, clock),
        [EntityCatalog.VendorPayments] = new PaymentRules(EntityCatalog.VendorPayments, PaymentFlow.Outgoing, clock),
    };

    public EntityRules For(EntityDefinition entity) => _rules.GetValueOrDefault(entity, NoRules);

    private sealed class PlainRules : EntityRules;
}
