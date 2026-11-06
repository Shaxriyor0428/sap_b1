namespace MockSl.Domain.Metadata;

/// <summary>
/// Columns that exist in the tables but not in the API, according to the EDMX.
/// The rules maintain them, and the SQL tasks read them directly.
/// </summary>
public static class StoredOnlyColumns
{
    public static readonly FieldDefinition InstallmentPaidToDate = FieldDefinition.Decimal("PaidToDate");

    public static readonly FieldDefinition InstallmentStatus = FieldDefinition.Text("Status", maxLength: 1);

    public static readonly FieldDefinition PaymentDocTotal = FieldDefinition.Decimal("DocTotal");
}
