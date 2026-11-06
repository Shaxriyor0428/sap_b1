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

    public static readonly EntityDefinition EmployeesInfo = new(
        setName: "EmployeesInfo",
        typeName: "EmployeeInfo",
        table: "OHEM",
        keyFields: ["EmployeeID"],
        fields:
        [
            FieldDefinition.Integer("EmployeeID", column: "empID") with { IsReadOnly = true },
            FieldDefinition.Text("FirstName", maxLength: 50, column: "firstName") with { IsRequired = true },
            FieldDefinition.Text("LastName", maxLength: 50, column: "lastName") with { IsRequired = true },
            FieldDefinition.Text("JobTitle", maxLength: 20, column: "jobTitle"),
            FieldDefinition.Enumeration("Active", SapEnums.BoYesNoEnum),
        ]);

    public static readonly EntityDefinition Items = new(
        setName: "Items",
        typeName: "Item",
        table: "OITM",
        keyFields: ["ItemCode"],
        fields:
        [
            FieldDefinition.Text("ItemCode", maxLength: 50) with { IsRequired = true },
            FieldDefinition.Text("ItemName", maxLength: 200),
            FieldDefinition.Enumeration("InventoryItem", SapEnums.BoYesNoEnum, column: "InvntItem"),
            FieldDefinition.Enumeration("SalesItem", SapEnums.BoYesNoEnum, column: "SellItem"),
            FieldDefinition.Enumeration("PurchaseItem", SapEnums.BoYesNoEnum, column: "PrchseItem"),

            // Stock changes only through documents, so it cannot be written directly.
            FieldDefinition.Decimal("QuantityOnStock", column: "OnHand") with { IsReadOnly = true },
        ],
        duplicateKeyMessage: key => $"Item code '{key}' already exists",
        children:
        [
            new ChildDefinition(
                name: "ItemWarehouseInfoCollection",
                typeName: "ItemWarehouseInfo",
                table: "OITW",
                parentKeyColumn: "ItemCode",
                rowKeyField: "WarehouseCode",
                firstRowKey: 0,
                fields:
                [
                    FieldDefinition.Text("ItemCode", maxLength: 50),
                    FieldDefinition.Text("WarehouseCode", maxLength: 8, column: "WhsCode"),
                    FieldDefinition.Decimal("InStock", column: "OnHand"),
                ],
                isReadOnly: true),
        ]);

    public static readonly EntityDefinition ChartOfAccounts = new(
        setName: "ChartOfAccounts",
        typeName: "ChartOfAccount",
        table: "OACT",
        keyFields: ["Code"],
        fields:
        [
            FieldDefinition.Text("Code", maxLength: 15, column: "AcctCode"),
            FieldDefinition.Text("Name", maxLength: 200, column: "AcctName") with { IsRequired = true },
            FieldDefinition.Text("FormatCode", maxLength: 210),
            FieldDefinition.Decimal("Balance", column: "CurrTotal") with { IsReadOnly = true },
            FieldDefinition.Enumeration("CashAccount", SapEnums.BoYesNoEnum, column: "Finanse"),
            FieldDefinition.Enumeration("ActiveAccount", SapEnums.BoYesNoEnum, column: "Postable"),
            FieldDefinition.Text("FatherAccountKey", maxLength: 15, column: "FatherNum"),
            FieldDefinition.Integer("AccountLevel", column: "Levels") with { IsReadOnly = true },
            FieldDefinition.Enumeration("AccountType", SapEnums.BoAccountTypes, column: "ActType"),
        ]);

    private static readonly IReadOnlyList<NavigationDefinition> ToBusinessPartner =
        [new NavigationDefinition("BusinessPartner", BusinessPartners, "CardCode")];

    public static readonly EntityDefinition Invoices =
        DocumentDefinitions.CreateDocument("Invoices", "OINV", "INV1", installmentsTable: "INV6", ToBusinessPartner);

    public static readonly EntityDefinition PurchaseInvoices =
        DocumentDefinitions.CreateDocument("PurchaseInvoices", "OPCH", "PCH1", installmentsTable: null, ToBusinessPartner);

    /// <summary>The task asks for BusinessPartner here. The EDMX has no such link on Payment, so real SAP may refuse it.</summary>
    public static readonly EntityDefinition IncomingPayments =
        DocumentDefinitions.CreatePayment("IncomingPayments", "ORCT", "RCT2", ToBusinessPartner);

    public static readonly EntityDefinition VendorPayments =
        DocumentDefinitions.CreatePayment("VendorPayments", "OVPM", "VPM2", navigations: []);

    /// <summary>Metadata of user-defined fields (guide p.86-89). Creating one adds a column, see UserFieldRules.</summary>
    public static readonly EntityDefinition UserFieldsMD = new(
        setName: "UserFieldsMD",
        typeName: "UserFieldMD",
        table: "CUFD",
        keyFields: ["TableName", "FieldID"],
        fields:
        [
            FieldDefinition.Text("Name", maxLength: 50, column: "AliasID") with { IsRequired = true, IsCreateOnly = true },
            FieldDefinition.Enumeration("Type", SapEnums.BoFieldTypes, column: "TypeID") with { IsRequired = true, IsCreateOnly = true },
            FieldDefinition.Integer("Size", column: "SizeID") with { IsCreateOnly = true },
            FieldDefinition.Text("Description", maxLength: 80, column: "Descr"),
            FieldDefinition.Enumeration("SubType", SapEnums.BoFldSubTypes, column: "EditType") with { IsCreateOnly = true },
            FieldDefinition.Text("LinkedTable", maxLength: 21, column: "RTable") with { IsReadOnly = true },
            FieldDefinition.Text("DefaultValue", maxLength: 254, column: "Dflt") with { IsReadOnly = true },
            FieldDefinition.Text("TableName", maxLength: 21, column: "TableID") with { IsRequired = true, IsCreateOnly = true },
            FieldDefinition.Integer("FieldID") with { IsReadOnly = true },
            FieldDefinition.Integer("EditSize"),
            FieldDefinition.Enumeration("Mandatory", SapEnums.BoYesNoEnum, column: "NotNull") with { IsReadOnly = true },
            FieldDefinition.Text("LinkedUDO", maxLength: 20, column: "RelUDO") with { IsReadOnly = true },
        ]);

    /// <summary>
    /// OACP: which account each kind of operation posts to. The EDMX has no entity set for it, only
    /// CompanyService_GetPeriods and CompanyService_GetPeriod, so it is not listed in Find below.
    /// Column mapping comes from the SDK property pages, for example AccountforCashReceipt is LinkAct_3.
    /// </summary>
    public static readonly EntityDefinition PeriodCategories = new(
        setName: "PeriodCategories",
        typeName: "PeriodCategory",
        table: "OACP",
        keyFields: ["AbsoluteEntry"],
        fields:
        [
            FieldDefinition.Integer("AbsoluteEntry", column: "AbsEntry") with { IsReadOnly = true },
            FieldDefinition.Text("PeriodCategoryProperty", maxLength: 10, column: "PeriodCat") with { IsReadOnly = true },
            FieldDefinition.Date("BeginningofFinancialYear", column: "FinancYear") with { IsReadOnly = true },
            FieldDefinition.Text("DebitorsFollowUpAccount", maxLength: 15, column: "LinkAct_1") with { IsReadOnly = true },
            FieldDefinition.Text("AccountforCashReceipt", maxLength: 15, column: "LinkAct_3") with { IsReadOnly = true },
            FieldDefinition.Text("DefaultSaleAccount", maxLength: 15, column: "DfltIncom") with { IsReadOnly = true },
            FieldDefinition.Text("ExpenseAccountDefault", maxLength: 15, column: "DfltExpn") with { IsReadOnly = true },
        ],
        canDelete: false);

    private static readonly EntityDefinition[] BusinessObjects =
    [
        BusinessPartners,
        EmployeesInfo,
        Items,
        ChartOfAccounts,
        Invoices,
        PurchaseInvoices,
        IncomingPayments,
        VendorPayments,
    ];

    private static readonly Dictionary<string, EntityDefinition> BySetName =
        BusinessObjects.Append(UserFieldsMD).ToDictionary(entity => entity.SetName);

    /// <summary>
    /// The only tables a user-defined field can be added to. The name goes into DDL, where
    /// parameters cannot be used, so this list is what keeps request text out of the statement.
    /// </summary>
    public static readonly IReadOnlySet<string> TablesWithUserFields =
        BusinessObjects.Select(entity => entity.Table).ToHashSet();

    public static EntityDefinition? Find(string setName) => BySetName.GetValueOrDefault(setName);
}
