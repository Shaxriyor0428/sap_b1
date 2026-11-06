using Sap.Application.Auth;
using Sap.Application.Customers;
using Sap.Application.Errors;
using Sap.Application.ServiceLayer;
using Sap.UnitTests.Fakes;

namespace Sap.UnitTests.Customers;

public class CustomerServiceTests
{
    private static readonly SlCookies Cookies = new("sl-session", ".node1");

    private static readonly UserSession Session = new(
        "token",
        "manager",
        Cookies,
        TimeSpan.FromMinutes(30),
        DateTimeOffset.UnixEpoch);

    private static CustomerQuery Query(int page = 1, int pageSize = 20) => new(null, null, null, page, pageSize);

    [Theory]
    [InlineData(1, 20, 21, 0)]
    [InlineData(3, 20, 21, 40)]
    [InlineData(2, 5, 6, 5)]
    public async Task List_asks_for_one_row_more_than_a_page(int page, int pageSize, int expectedTop, long expectedSkip)
    {
        var serviceLayer = new FakeServiceLayer();

        await new CustomerService(serviceLayer).ListAsync(Session, Query(page, pageSize), default);

        Assert.Equal(expectedTop, serviceLayer.LastQuery!.Top);
        Assert.Equal(expectedSkip, serviceLayer.LastQuery.Skip);
    }

    [Fact]
    public async Task List_has_a_stable_order_so_pages_do_not_overlap()
    {
        var serviceLayer = new FakeServiceLayer();

        await new CustomerService(serviceLayer).ListAsync(Session, Query(), default);

        Assert.Equal("CardCode", serviceLayer.LastQuery!.OrderBy);
    }

    [Fact]
    public async Task List_uses_the_session_of_the_current_user()
    {
        var serviceLayer = new FakeServiceLayer();

        await new CustomerService(serviceLayer).ListAsync(Session, Query(), default);

        Assert.Equal(Cookies, serviceLayer.LastCookies);
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(19, 19, false)]
    [InlineData(20, 20, false)]
    [InlineData(21, 20, true)]
    public async Task List_returns_one_page_and_detects_the_next(int rowsFound, int expectedItems, bool expectedNext)
    {
        var service = new CustomerService(new FakeServiceLayer { PartnerCount = rowsFound });

        var page = await service.ListAsync(Session, Query(page: 2), default);

        Assert.Equal(expectedItems, page.Items.Count);
        Assert.Equal(expectedNext, page.HasNextPage);
        Assert.Equal(2, page.Page);
        Assert.Equal(20, page.PageSize);
    }

    [Fact]
    public async Task List_hides_service_layer_names_behind_its_own()
    {
        var service = new CustomerService(new FakeServiceLayer { PartnerCount = 1 });

        var customer = (await service.ListAsync(Session, Query(), default)).Items[0];

        Assert.Equal(new Customer("C1", "Name 1", "+998", "+999", "a@b.uz", "Street", "City", 10m), customer);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    [InlineData(1, 101)]
    public async Task List_rejects_bad_paging_without_calling_the_service_layer(int page, int pageSize)
    {
        var serviceLayer = new FakeServiceLayer();

        await Assert.ThrowsAsync<InvalidRequestException>(() =>
            new CustomerService(serviceLayer).ListAsync(Session, Query(page, pageSize), default));

        Assert.Null(serviceLayer.LastQuery);
    }

    [Fact]
    public async Task Very_large_page_number_does_not_overflow()
    {
        var serviceLayer = new FakeServiceLayer();

        await new CustomerService(serviceLayer).ListAsync(Session, Query(page: int.MaxValue, pageSize: 100), default);

        Assert.True(serviceLayer.LastQuery!.Skip > 0);
    }

    [Fact]
    public async Task Create_always_makes_a_customer_and_maps_the_fields()
    {
        var serviceLayer = new FakeServiceLayer();
        var customer = new NewCustomer("C1", "Ali", "+998", "+999", "a@b.uz", "Street", "City");

        var created = await new CustomerService(serviceLayer).CreateAsync(Session, customer, default);

        Assert.Equal(
            new BusinessPartnerWrite
            {
                CardCode = "C1",
                CardName = "Ali",
                CardType = "cCustomer",
                Phone1 = "+998",
                Cellular = "+999",
                EmailAddress = "a@b.uz",
                Address = "Street",
                City = "City",
            },
            serviceLayer.LastWrite);
        Assert.Equal("C1", created.Code);
    }

    [Fact]
    public async Task Update_sends_only_the_fields_that_were_given()
    {
        var serviceLayer = new FakeServiceLayer();

        await new CustomerService(serviceLayer).UpdateAsync(
            Session,
            "C1",
            new CustomerChanges("New name", null, null, null, null, null),
            default);

        Assert.Equal(new BusinessPartnerWrite { CardName = "New name" }, serviceLayer.LastWrite);
    }

    [Theory]
    [InlineData("cSupplier")]
    [InlineData("cLid")]
    public async Task Update_refuses_a_partner_that_is_not_a_customer(string cardType)
    {
        var serviceLayer = new FakeServiceLayer { CardType = cardType };

        await Assert.ThrowsAsync<CustomerNotFoundException>(() =>
            new CustomerService(serviceLayer).UpdateAsync(Session, "S1", new CustomerChanges("x", null, null, null, null, null), default));

        Assert.False(serviceLayer.UpdateCalled);
    }

    [Theory]
    [InlineData("cSupplier")]
    [InlineData("cLid")]
    public async Task Delete_refuses_a_partner_that_is_not_a_customer(string cardType)
    {
        var serviceLayer = new FakeServiceLayer { CardType = cardType };

        await Assert.ThrowsAsync<CustomerNotFoundException>(() =>
            new CustomerService(serviceLayer).DeleteAsync(Session, "S1", default));

        Assert.False(serviceLayer.DeleteCalled);
    }

    [Fact]
    public async Task Delete_removes_a_customer()
    {
        var serviceLayer = new FakeServiceLayer();

        await new CustomerService(serviceLayer).DeleteAsync(Session, "C1", default);

        Assert.Equal("C1", serviceLayer.LastCardCode);
        Assert.True(serviceLayer.DeleteCalled);
    }
}
