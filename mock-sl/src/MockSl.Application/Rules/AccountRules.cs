using MockSl.Application.Entities;
using MockSl.Domain.Errors;
using MockSl.Domain.Metadata;

namespace MockSl.Application.Rules;

public sealed class AccountRules : EntityRules
{
    private static readonly EntityDefinition Accounts = EntityCatalog.ChartOfAccounts;

    public override async Task BeforeInsertAsync(
        EntityDraft draft,
        IEntitySession session,
        CancellationToken cancellationToken)
    {
        var header = draft.Header;

        // Real SAP samples create an account from FormatCode and return the Code (B1 API Cookbook).
        var code = header.Text("AcctCode") ?? header.Text("FormatCode")
            ?? throw SapErrors.RequiredProperty(Accounts.TypeName, "Code");

        header.Set(Accounts.Field("Code"), code);
        header.Set(Accounts.Field("AccountLevel"), await LevelUnderParentAsync(header.Text("FatherNum"), session, cancellationToken));
    }

    private static async Task<int> LevelUnderParentAsync(
        string? parentCode,
        IEntitySession session,
        CancellationToken cancellationToken)
    {
        if (parentCode is null)
        {
            return 1;
        }

        var parent = await session.FindAsync(
            Accounts,
            EntityKey.Of(Accounts, parentCode),
            [Accounts.Field("AccountLevel")],
            cancellationToken)
            ?? throw SapErrors.ReferenceNotFound("FatherAccountKey", parentCode);

        return parent.Int("Levels") + 1;
    }
}
