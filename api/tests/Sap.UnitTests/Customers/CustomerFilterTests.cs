using Sap.Application.Customers;

namespace Sap.UnitTests.Customers;

public class CustomerFilterTests
{
    private static string Build(string? name = null, string? phone = null, string? address = null) =>
        CustomerFilter.Build(new CustomerQuery(name, phone, address, Page: 1, PageSize: 20));

    [Fact]
    public void Without_parameters_only_customers_are_requested()
    {
        Assert.Equal("CardType eq 'cCustomer'", Build());
    }

    [Theory]
    [InlineData("Ali", null, null, "contains(CardName,'Ali')")]
    [InlineData(null, "90", null, "contains(Phone1,'90')")]
    [InlineData(null, null, "Navoiy", "contains(Address,'Navoiy')")]
    public void Each_parameter_maps_to_its_own_field(string? name, string? phone, string? address, string expected)
    {
        Assert.Equal($"CardType eq 'cCustomer' and {expected}", Build(name, phone, address));
    }

    [Fact]
    public void Given_parameters_are_joined_with_and()
    {
        Assert.Equal(
            "CardType eq 'cCustomer' and contains(CardName,'Ali') and contains(Phone1,'90') and contains(Address,'Navoiy')",
            Build("Ali", "90", "Navoiy"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_parameter_adds_no_condition(string name)
    {
        Assert.Equal("CardType eq 'cCustomer'", Build(name));
    }

    [Fact]
    public void Apostrophe_in_a_value_is_doubled()
    {
        Assert.Equal("CardType eq 'cCustomer' and contains(CardName,'O''Brien')", Build("O'Brien"));
    }

    [Fact]
    public void A_value_cannot_close_the_string_and_widen_the_filter()
    {
        var filter = Build("x') or (CardType eq 'cSupplier");

        // The whole input stays inside one string literal: every quote from the user is doubled.
        Assert.Equal("CardType eq 'cCustomer' and contains(CardName,'x'') or (CardType eq ''cSupplier')", filter);
    }
}
