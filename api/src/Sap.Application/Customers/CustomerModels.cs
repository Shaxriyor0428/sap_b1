namespace Sap.Application.Customers;

/// <summary>A customer as this API shows it. Service Layer names stay behind the mapping in CustomerService.</summary>
public sealed record Customer(
    string Code,
    string? Name,
    string? Phone,
    string? Cellular,
    string? Email,
    string? Address,
    string? City,
    decimal? Balance,
    string? Region);

public sealed record CustomerPage(IReadOnlyList<Customer> Items, int Page, int PageSize, bool HasNextPage);

public sealed record CustomerQuery(string? Name, string? Phone, string? Address, string? Region, int Page, int PageSize);

/// <summary>Nothing is required here on purpose: the Service Layer owns the rules and reports what is missing.</summary>
public sealed record NewCustomer(
    string? Code,
    string? Name,
    string? Phone,
    string? Cellular,
    string? Email,
    string? Address,
    string? City);

/// <summary>Only the fields that are not null are changed.</summary>
public sealed record CustomerChanges(
    string? Name,
    string? Phone,
    string? Cellular,
    string? Email,
    string? Address,
    string? City);
