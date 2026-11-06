namespace MockSl.Domain.Metadata;

/// <summary>Enum names come from the EDMX, database values from the SDK table reference.</summary>
public static class SapEnums
{
    public static readonly EnumMap BoCardTypes = new(
        ("cCustomer", "C"),
        ("cSupplier", "S"),
        ("cLid", "L"));
}
