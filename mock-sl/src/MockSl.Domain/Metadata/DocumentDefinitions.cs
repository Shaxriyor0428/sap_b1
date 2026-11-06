namespace MockSl.Domain.Metadata;

/// <summary>
/// Sales and purchase documents share one shape (type "Document" in the EDMX), and so do
/// incoming and outgoing payments (type "Payment"). Only the table names differ.
/// </summary>
internal static class DocumentDefinitions
{
    public static EntityDefinition CreateDocument(
        string setName,
        string table,
        string linesTable,
        string? installmentsTable)
    {
        var children = new List<ChildDefinition> { CreateLines(linesTable) };
        if (installmentsTable is not null)
        {
            children.Add(CreateInstallments(installmentsTable));
        }

        return new EntityDefinition(
            setName,
            typeName: "Document",
            table,
            keyFields: ["DocEntry"],
            fields:
            [
                FieldDefinition.Integer("DocEntry") with { IsReadOnly = true },
                FieldDefinition.Integer("DocNum") with { IsReadOnly = true },
                FieldDefinition.Enumeration("DocType", SapEnums.BoDocumentTypes) with { IsReadOnly = true },
                FieldDefinition.Date("DocDate") with { IsCreateOnly = true },
                FieldDefinition.Date("DocDueDate"),
                FieldDefinition.Text("CardCode", maxLength: 15) with { IsCreateOnly = true, IsRequired = true },
                FieldDefinition.Text("CardName", maxLength: 200) with { IsReadOnly = true },
                FieldDefinition.Decimal("DocTotal") with { IsReadOnly = true },
                FieldDefinition.Decimal("PaidToDate") with { IsReadOnly = true },
                FieldDefinition.Enumeration("DocumentStatus", SapEnums.BoStatus, column: "DocStatus") with { IsReadOnly = true },
                FieldDefinition.Enumeration("Cancelled", SapEnums.BoYesNoEnum, column: "CANCELED") with { IsReadOnly = true },
                FieldDefinition.Enumeration("CancelStatus", SapEnums.CancelStatusEnum, column: "CANCELED") with { IsReadOnly = true },
                FieldDefinition.Integer("DocumentsOwner", column: "OwnerCode"),
                FieldDefinition.Integer("SalesPersonCode", column: "SlpCode"),
                FieldDefinition.Integer("PaymentGroupCode", column: "GroupNum"),
                FieldDefinition.Integer("NumberOfInstallments", column: "Installmnt") with { IsReadOnly = true },
                FieldDefinition.Text("Comments", maxLength: 254),
            ],
            children: children,
            canDelete: false);
    }

    public static EntityDefinition CreatePayment(string setName, string table, string invoicesTable) => new(
        setName,
        typeName: "Payment",
        table,
        keyFields: ["DocEntry"],
        fields:
        [
            FieldDefinition.Integer("DocEntry") with { IsReadOnly = true },
            FieldDefinition.Integer("DocNum") with { IsReadOnly = true },

            // The EDMX really has both names, the second one misspelled. Each set accepts one kind only.
            FieldDefinition.Enumeration("DocType", SapEnums.BoRcptTypes) with { IsReadOnly = true },
            FieldDefinition.Enumeration("DocTypte", SapEnums.BoRcptTypes, column: "DocType") with { IsReadOnly = true },
            FieldDefinition.Date("DocDate") with { IsCreateOnly = true },
            FieldDefinition.Text("CardCode", maxLength: 15) with { IsCreateOnly = true, IsRequired = true },
            FieldDefinition.Text("CardName", maxLength: 200) with { IsReadOnly = true },
            FieldDefinition.Text("CashAccount", maxLength: 15, column: "CashAcct") with { IsCreateOnly = true },
            FieldDefinition.Decimal("CashSum") with { IsCreateOnly = true, IsRequired = true },
            FieldDefinition.Enumeration("Cancelled", SapEnums.BoYesNoEnum, column: "Canceled") with { IsReadOnly = true },
            FieldDefinition.Text("Remarks", maxLength: 254, column: "Comments"),
        ],
        children:
        [
            new ChildDefinition(
                name: "PaymentInvoices",
                typeName: "PaymentInvoice",
                table: invoicesTable,
                parentKeyColumn: "DocNum",
                rowKeyField: "LineNum",
                firstRowKey: 0,
                fields:
                [
                    FieldDefinition.Integer("LineNum", column: "InvoiceId") with { IsReadOnly = true },

                    // Not the payment: in RCT2 and VPM2 DocEntry is the invoice, DocNum is the payment.
                    FieldDefinition.Integer("DocEntry") with { IsRequired = true },
                    FieldDefinition.Decimal("SumApplied") with { IsRequired = true },
                    FieldDefinition.Enumeration("InvoiceType", SapEnums.BoRcptInvTypes, column: "InvType"),
                    FieldDefinition.Integer("InstallmentId", column: "InstId"),
                ]),
        ],
        canDelete: false);

    private static ChildDefinition CreateLines(string table) => new(
        name: "DocumentLines",
        typeName: "DocumentLine",
        table,
        parentKeyColumn: "DocEntry",
        rowKeyField: "LineNum",
        firstRowKey: 0,
        fields:
        [
            FieldDefinition.Integer("LineNum") with { IsReadOnly = true },
            FieldDefinition.Text("ItemCode", maxLength: 50) with { IsRequired = true },
            FieldDefinition.Text("ItemDescription", maxLength: 200, column: "Dscription"),
            FieldDefinition.Decimal("Quantity") with { IsRequired = true },

            // Discounts are not built, so both names are the same column.
            FieldDefinition.Decimal("Price"),
            FieldDefinition.Decimal("UnitPrice", column: "Price"),
            FieldDefinition.Decimal("LineTotal") with { IsReadOnly = true },
            FieldDefinition.Text("WarehouseCode", maxLength: 8, column: "WhsCode"),
            FieldDefinition.Text("TaxCode", maxLength: 8),
            FieldDefinition.Text("VatGroup", maxLength: 8),
            FieldDefinition.Enumeration("LineStatus", SapEnums.BoStatus) with { IsReadOnly = true },
        ]);

    private static ChildDefinition CreateInstallments(string table) => new(
        name: "DocumentInstallments",
        typeName: "DocumentInstallment",
        table,
        parentKeyColumn: "DocEntry",
        rowKeyField: "InstallmentId",
        firstRowKey: 1,
        fields:
        [
            FieldDefinition.Integer("InstallmentId", column: "InstlmntID") with { IsReadOnly = true },
            FieldDefinition.Date("DueDate"),
            FieldDefinition.Decimal("Total", column: "InsTotal"),
            FieldDefinition.Decimal("Percentage", column: "InstPrcnt"),
        ]);
}
