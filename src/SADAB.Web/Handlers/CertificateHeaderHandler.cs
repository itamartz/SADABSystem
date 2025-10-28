using SADAB.Web.Services;

namespace SADAB.Web.Handlers;

/// <summary>
/// HTTP message handler that automatically adds the client certificate thumbprint header to all API requests
/// </summary>
public class CertificateHeaderHandler : DelegatingHandler
{
    private readonly ICertificateStorageService _certificateStorage;
    private readonly ILogger<CertificateHeaderHandler> _logger;
    private readonly string _certificateHeaderName;

    public CertificateHeaderHandler(
        ICertificateStorageService certificateStorage,
        IConfiguration configuration,
        ILogger<CertificateHeaderHandler> logger)
    {
        _certificateStorage = certificateStorage;
        _logger = logger;
        _certificateHeaderName = configuration["ServiceSettings:CertificateHeaderName"]
            ?? "X-Client-Certificate-Thumbprint";
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Get certificate thumbprint
        var thumbprint = _certificateStorage.GetCertificateThumbprint();

        if (!string.IsNullOrEmpty(thumbprint))
        {
            // Add certificate header to request
            request.Headers.Add(_certificateHeaderName, thumbprint);
            _logger.LogDebug("Added certificate header to request: {Uri}", request.RequestUri);
        }
        else
        {
            _logger.LogWarning("No certificate thumbprint available for request: {Uri}", request.RequestUri);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
