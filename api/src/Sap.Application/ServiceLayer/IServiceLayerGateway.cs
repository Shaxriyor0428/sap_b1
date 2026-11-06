namespace Sap.Application.ServiceLayer;

/// <summary>
/// Port to the Service Layer, implemented in Infrastructure over HTTP.
/// This code does not know whether the other side is the mock or real SAP.
/// </summary>
public interface IServiceLayerGateway
{
    Task<SlLogin> LoginAsync(string userName, string password, CancellationToken cancellationToken);

    Task LogoutAsync(SlCookies cookies, CancellationToken cancellationToken);

    Task<IReadOnlyList<BusinessPartner>> ListBusinessPartnersAsync(
        SlCookies cookies,
        BusinessPartnerQuery query,
        CancellationToken cancellationToken);

    /// <summary>Returns the CardType of one partner, for example cCustomer.</summary>
    Task<string?> GetCardTypeAsync(SlCookies cookies, string cardCode, CancellationToken cancellationToken);

    Task<BusinessPartner> CreateBusinessPartnerAsync(
        SlCookies cookies,
        BusinessPartnerWrite partner,
        CancellationToken cancellationToken);

    Task UpdateBusinessPartnerAsync(
        SlCookies cookies,
        string cardCode,
        BusinessPartnerWrite changes,
        CancellationToken cancellationToken);

    Task DeleteBusinessPartnerAsync(SlCookies cookies, string cardCode, CancellationToken cancellationToken);
}
