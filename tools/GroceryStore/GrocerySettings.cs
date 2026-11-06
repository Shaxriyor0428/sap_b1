using System.Globalization;

namespace GroceryStore;

/// <summary>
/// Everything that differs between the mock and a real company database, read from the environment.
/// Nothing specific to one system is written in the code.
/// </summary>
public sealed record GrocerySettings(
    string BaseUrl,
    string CompanyDb,
    bool SkipTlsVerify,
    string UserName,
    string Password,
    string Warehouse,
    string? TaxCode,
    string? VatGroup,
    string Prefix,
    DateOnly CurrentMonth,
    string CashParentAccount)
{
    public DateOnly PreviousMonth => CurrentMonth.AddMonths(-1);

    public static GrocerySettings FromEnvironment() => new(
        Required("SL_BASE_URL"),
        Required("SL_COMPANY_DB"),
        string.Equals(Optional("SL_SKIP_TLS_VERIFY"), "true", StringComparison.OrdinalIgnoreCase),
        Required("SL_USERNAME"),
        Required("SL_PASSWORD"),
        Optional("SL_WAREHOUSE") ?? "01",
        Optional("SL_TAX_CODE"),
        Optional("SL_VAT_GROUP"),
        Optional("GROCERY_PREFIX") ?? "G",

        // A fixed month, not today: the expected results in the sql folder must not change next month.
        DateOnly.ParseExact(Optional("GROCERY_CURRENT_MONTH") ?? "2026-11", "yyyy-MM", CultureInfo.InvariantCulture),
        Required("GROCERY_CASH_PARENT_ACCOUNT"));

    private static string Required(string name) =>
        Optional(name) ?? throw new InvalidOperationException($"Setting {name} is missing.");

    private static string? Optional(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
