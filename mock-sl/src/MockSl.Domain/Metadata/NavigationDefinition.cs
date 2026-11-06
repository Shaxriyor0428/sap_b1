namespace MockSl.Domain.Metadata;

/// <summary>
/// A link from one object to another, read with $expand. For example, a sale has
/// BusinessPartner: its CardCode points to the partner's key (guide p.81-83).
/// </summary>
/// <param name="Name">The name in the URL and in the JSON answer.</param>
/// <param name="Target">The object it leads to.</param>
/// <param name="ForeignKeyField">The field of this object that holds the target's key.</param>
public sealed record NavigationDefinition(string Name, EntityDefinition Target, string ForeignKeyField);
