namespace Sap.Application.Customers;

/// <summary>Builds the OData $filter for the customer list. Filters that were given are joined with "and".</summary>
public static class CustomerFilter
{
    public const string CustomerCardType = "cCustomer";

    public static string Build(CustomerQuery query)
    {
        var conditions = new List<string> { $"CardType eq '{CustomerCardType}'" };

        AddContains(conditions, "CardName", query.Name);
        AddContains(conditions, "Phone1", query.Phone);
        AddContains(conditions, "Address", query.Address);

        return string.Join(" and ", conditions);
    }

    private static void AddContains(List<string> conditions, string field, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            conditions.Add($"contains({field},{Literal(value)})");
        }
    }

    // A quote is doubled in OData, as in SQL. Without this, a user could close the string
    // and add conditions of their own, for example to see every business partner.
    private static string Literal(string value) => $"'{value.Replace("'", "''")}'";
}
