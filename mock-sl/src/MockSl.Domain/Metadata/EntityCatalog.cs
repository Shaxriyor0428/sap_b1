namespace MockSl.Domain.Metadata;

/// <summary>All objects the mock exposes. Field mapping is documented in docs/sl-contract.md.</summary>
public static class EntityCatalog
{
    public static readonly EntityDefinition BusinessPartners = new(
        setName: "BusinessPartners",
        typeName: "BusinessPartner",
        table: "OCRD",
        keyFields: ["CardCode"],
        fields:
        [
            FieldDefinition.Text("CardCode", maxLength: 15) with { IsRequired = true },
            FieldDefinition.Text("CardName", maxLength: 200),
            FieldDefinition.Enumeration("CardType", SapEnums.BoCardTypes),
            FieldDefinition.Text("Phone1", maxLength: 50),
            FieldDefinition.Text("Cellular", maxLength: 50),
            FieldDefinition.Text("EmailAddress", maxLength: 100, column: "E_Mail"),
            FieldDefinition.Text("Address", maxLength: 100),
            FieldDefinition.Text("City", maxLength: 100),
            FieldDefinition.Decimal("CurrentAccountBalance", column: "Balance") with { IsReadOnly = true },
        ],
        duplicateKeyMessage: key =>
            $"1320000140 - Business partner code '{key}' already assigned to a business partner; "
            + "enter a unique business partner code");

    private static readonly Dictionary<string, EntityDefinition> BySetName = new[]
    {
        BusinessPartners,
    }.ToDictionary(entity => entity.SetName);

    public static EntityDefinition? Find(string setName) => BySetName.GetValueOrDefault(setName);
}
