using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Sap.Application.Errors;
using Sap.Infrastructure.ServiceLayer;

namespace Sap.UnitTests.ServiceLayer;

/// <summary>
/// Real Service Layer usually has a self-signed certificate, so SL_SKIP_TLS_VERIFY decides whether the
/// mentor's check can connect at all. These tests use a real HTTPS server with such a certificate.
/// </summary>
public sealed class TlsVerificationTests : IAsyncLifetime
{
    private WebApplication? _server;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(kestrel =>
            kestrel.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(CreateSelfSignedCertificate())));

        _server = builder.Build();
        _server.MapPost("/b1s/v2/Login", () =>
            Results.Text("""{"SessionId":"abc","SessionTimeout":30}""", "application/json"));

        await _server.StartAsync();
    }

    public async Task DisposeAsync() => await _server!.DisposeAsync();

    [Fact]
    public async Task Self_signed_certificate_is_rejected_by_default()
    {
        var error = await Assert.ThrowsAsync<ServiceLayerUnavailableException>(() =>
            NewClient(skipTlsVerify: false).LoginAsync("manager", "secret", default));

        Assert.IsType<HttpRequestException>(error.InnerException);
    }

    [Fact]
    public async Task Self_signed_certificate_is_accepted_when_the_setting_is_on()
    {
        var login = await NewClient(skipTlsVerify: true).LoginAsync("manager", "secret", default);

        Assert.Equal("abc", login.Cookies.B1Session);
    }

    private ServiceLayerClient NewClient(bool skipTlsVerify)
    {
        var address = _server!.Urls.Single();
        var options = ServiceLayerOptions.Create($"{address}/b1s/v2", "TEST", skipTlsVerify);
        var http = new HttpClient(ServiceLayerHttpHandler.Create(skipTlsVerify)) { BaseAddress = options.BaseAddress };
        return new ServiceLayerClient(http, options);
    }

    private static X509Certificate2 CreateSelfSignedCertificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

        // Reloading from PFX gives a certificate whose private key the TLS layer can use on every platform.
        return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pfx), password: null);
    }
}
