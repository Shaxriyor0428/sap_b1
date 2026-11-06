using Sap.Application.ServiceLayer;

namespace Sap.UnitTests.Fakes;

/// <summary>Stands in for the Service Layer, so business logic is tested without HTTP.</summary>
public sealed class FakeServiceLayer : IServiceLayerGateway
{
    public SlLogin LoginResult { get; init; } = new(new SlCookies("sl-session", ".node1"), 30);

    public Exception? LogoutError { get; init; }

    public int PartnerCount { get; init; }

    public string? CardType { get; init; } = "cCustomer";

    public BusinessPartnerQuery? LastQuery { get; private set; }

    public BusinessPartnerWrite? LastWrite { get; private set; }

    public string? LastCardCode { get; private set; }

    public SlCookies? LastCookies { get; private set; }

    public bool DeleteCalled { get; private set; }

    public bool UpdateCalled { get; private set; }

    public bool LogoutCalled { get; private set; }

    public Task<SlLogin> LoginAsync(string userName, string password, CancellationToken cancellationToken) =>
        Task.FromResult(LoginResult);

    public Task LogoutAsync(SlCookies cookies, CancellationToken cancellationToken)
    {
        LogoutCalled = true;
        return LogoutError is null ? Task.CompletedTask : Task.FromException(LogoutError);
    }

    public Task<IReadOnlyList<BusinessPartner>> ListBusinessPartnersAsync(
        SlCookies cookies,
        BusinessPartnerQuery query,
        CancellationToken cancellationToken)
    {
        LastCookies = cookies;
        LastQuery = query;

        IReadOnlyList<BusinessPartner> partners = Enumerable.Range(1, PartnerCount)
            .Select(number => new BusinessPartner($"C{number}", $"Name {number}", "cCustomer", "+998", "+999", "a@b.uz", "Street", "City", 10m))
            .ToList();

        return Task.FromResult(partners);
    }

    public Task<string?> GetCardTypeAsync(SlCookies cookies, string cardCode, CancellationToken cancellationToken)
    {
        LastCardCode = cardCode;
        return Task.FromResult(CardType);
    }

    public Task<BusinessPartner> CreateBusinessPartnerAsync(
        SlCookies cookies,
        BusinessPartnerWrite partner,
        CancellationToken cancellationToken)
    {
        LastWrite = partner;
        return Task.FromResult(new BusinessPartner(partner.CardCode!, partner.CardName, partner.CardType, partner.Phone1, partner.Cellular, partner.EmailAddress, partner.Address, partner.City, 0m));
    }

    public Task UpdateBusinessPartnerAsync(
        SlCookies cookies,
        string cardCode,
        BusinessPartnerWrite changes,
        CancellationToken cancellationToken)
    {
        UpdateCalled = true;
        LastWrite = changes;
        return Task.CompletedTask;
    }

    public Task DeleteBusinessPartnerAsync(SlCookies cookies, string cardCode, CancellationToken cancellationToken)
    {
        DeleteCalled = true;
        return Task.CompletedTask;
    }
}
