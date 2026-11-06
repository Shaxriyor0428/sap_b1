namespace MockSl.Domain.Metadata;

/// <summary>Enum names come from the EDMX, database values from the SDK table reference.</summary>
public static class SapEnums
{
    public static readonly EnumMap BoCardTypes = new(
        ("cCustomer", "C"),
        ("cSupplier", "S"),
        ("cLid", "L"));

    public static readonly EnumMap BoYesNoEnum = new(
        ("tYES", "Y"),
        ("tNO", "N"));

    public static readonly EnumMap BoStatus = new(
        ("bost_Open", "O"),
        ("bost_Close", "C"));

    public static readonly EnumMap BoDocumentTypes = new(
        ("dDocument_Items", "I"),
        ("dDocument_Service", "S"));

    public static readonly EnumMap CancelStatusEnum = new(
        ("csYes", "Y"),
        ("csNo", "N"),
        ("csCancellation", "C"));

    public static readonly EnumMap BoRcptTypes = new(
        ("rCustomer", "C"),
        ("rAccount", "A"),
        ("rSupplier", "S"));

    public static readonly EnumMap BoRcptInvTypes = new(
        ("it_Invoice", "13"),
        ("it_PurchaseInvoice", "18"));

    public static readonly EnumMap BoAccountTypes = new(
        ("at_Revenues", "I"),
        ("at_Expenses", "E"),
        ("at_Other", "N"));

    /// <summary>The letters are a mock choice: the SDK pages listing them could not be opened.</summary>
    public static readonly EnumMap BoFieldTypes = new(
        ("db_Alpha", "A"),
        ("db_Memo", "M"),
        ("db_Numeric", "N"),
        ("db_Date", "D"),
        ("db_Float", "B"));

    /// <summary>The EDMX gives these as character codes, for example st_Phone=35, which is '#'.</summary>
    public static readonly EnumMap BoFldSubTypes = new(
        ("st_None", string.Empty),
        ("st_Address", "?"),
        ("st_Phone", "#"),
        ("st_Time", "T"),
        ("st_Rate", "R"),
        ("st_Sum", "S"),
        ("st_Price", "P"),
        ("st_Quantity", "Q"),
        ("st_Percentage", "%"),
        ("st_Measurement", "M"),
        ("st_Link", "B"),
        ("st_Image", "I"));
}
