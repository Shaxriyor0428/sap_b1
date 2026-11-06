namespace Sap.Infrastructure.ServiceLayer;

public static class ServiceLayerHttpHandler
{
    public static SocketsHttpHandler Create(bool skipTlsVerify)
    {
        var handler = new SocketsHttpHandler
        {
            // The handler is shared by all users. A shared cookie store would send one user's
            // B1SESSION on behalf of another, so cookies are set by hand on every request.
            UseCookies = false,
        };

        if (skipTlsVerify)
        {
            // Real Service Layer usually runs on port 50000 with a self-signed certificate.
            handler.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;
        }

        return handler;
    }
}
