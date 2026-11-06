using Sap.Application.Auth;
using Sap.Application.Errors;
using Sap.Application.ServiceLayer;

namespace Sap.Application.Customers;

/// <summary>
/// The customer use cases. Business rules such as "the code must be unique" live in the
/// Service Layer, so its errors are passed on instead of being repeated here.
/// </summary>
public sealed class CustomerService(IServiceLayerGateway serviceLayer)
{
    public const int DefaultPageSize = 20;

    public const int MaxPageSize = 100;

    public async Task<CustomerPage> ListAsync(
        UserSession session,
        CustomerQuery query,
        CancellationToken cancellationToken)
    {
        Validate(query);

        // One row more than a page tells whether a next page exists. This does not depend on how
        // the Service Layer combines $top with @odata.nextLink, which the guide does not specify.
        var serviceLayerQuery = new BusinessPartnerQuery(
            Filter: CustomerFilter.Build(query),
            OrderBy: "CardCode",
            Top: query.PageSize + 1,
            Skip: (query.Page - 1L) * query.PageSize);

        var partners = await serviceLayer.ListBusinessPartnersAsync(session.Cookies, serviceLayerQuery, cancellationToken);

        var items = partners.Take(query.PageSize).Select(ToCustomer).ToList();
        return new CustomerPage(items, query.Page, query.PageSize, HasNextPage: partners.Count > query.PageSize);
    }

    public async Task<Customer> CreateAsync(
        UserSession session,
        NewCustomer customer,
        CancellationToken cancellationToken)
    {
        var partner = new BusinessPartnerWrite
        {
            CardCode = customer.Code,
            CardName = customer.Name,
            CardType = CustomerFilter.CustomerCardType,
            Phone1 = customer.Phone,
            Cellular = customer.Cellular,
            EmailAddress = customer.Email,
            Address = customer.Address,
            City = customer.City,
        };

        var created = await serviceLayer.CreateBusinessPartnerAsync(session.Cookies, partner, cancellationToken);
        return ToCustomer(created);
    }

    public async Task UpdateAsync(
        UserSession session,
        string code,
        CustomerChanges changes,
        CancellationToken cancellationToken)
    {
        await EnsureIsCustomerAsync(session, code, cancellationToken);

        var partner = new BusinessPartnerWrite
        {
            CardName = changes.Name,
            Phone1 = changes.Phone,
            Cellular = changes.Cellular,
            EmailAddress = changes.Email,
            Address = changes.Address,
            City = changes.City,
        };

        await serviceLayer.UpdateBusinessPartnerAsync(session.Cookies, code, partner, cancellationToken);
    }

    public async Task DeleteAsync(UserSession session, string code, CancellationToken cancellationToken)
    {
        await EnsureIsCustomerAsync(session, code, cancellationToken);
        await serviceLayer.DeleteBusinessPartnerAsync(session.Cookies, code, cancellationToken);
    }

    // OCRD holds customers, suppliers and leads together. Without this check,
    // the customer API could change or delete a supplier just by using its code.
    private async Task EnsureIsCustomerAsync(UserSession session, string code, CancellationToken cancellationToken)
    {
        var cardType = await serviceLayer.GetCardTypeAsync(session.Cookies, code, cancellationToken);

        if (cardType != CustomerFilter.CustomerCardType)
        {
            throw new CustomerNotFoundException(code);
        }
    }

    private static void Validate(CustomerQuery query)
    {
        if (query.Page < 1)
        {
            throw new InvalidRequestException("page must be 1 or greater.");
        }

        if (query.PageSize is < 1 or > MaxPageSize)
        {
            throw new InvalidRequestException($"pageSize must be between 1 and {MaxPageSize}.");
        }
    }

    private static Customer ToCustomer(BusinessPartner partner) => new(
        partner.CardCode,
        partner.CardName,
        partner.Phone1,
        partner.Cellular,
        partner.EmailAddress,
        partner.Address,
        partner.City,
        partner.CurrentAccountBalance);
}
