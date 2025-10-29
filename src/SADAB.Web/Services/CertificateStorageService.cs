using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace SADAB.Web.Services;

/// <summary>
/// Implementation of certificate storage service that stores certificates in the application's data directory
/// </summary>
public class CertificateStorageService : ICertificateStorageService
{
    private readonly string _certificatePath;
    private readonly string _privateKeyPath;
    private readonly ILogger<CertificateStorageService> _logger;

    public CertificateStorageService(IConfiguration configuration, ILogger<CertificateStorageService> logger)
    {
        _logger = logger;

        // Store certificates in app data directory
        var dataPath = configuration["CertificateSettings:StoragePath"]
            ?? Path.Combine(AppContext.BaseDirectory, "Data");

        if (!Directory.Exists(dataPath))
        {
            Directory.CreateDirectory(dataPath);
            _logger.LogInformation("Created certificate storage directory at {Path}", dataPath);
        }

        _certificatePath = Path.Combine(dataPath, "webapp.cert");
        _privateKeyPath = Path.Combine(dataPath, "webapp.key");
    }

    public string? GetCertificateThumbprint()
    {
        try
        {
            if (!File.Exists(_certificatePath))
            {
                _logger.LogDebug("Certificate file not found at {Path}", _certificatePath);
                return null;
            }

            var certContent = File.ReadAllText(_certificatePath);
            var cert = X509Certificate2.CreateFromPem(certContent);
            var thumbprint = cert.Thumbprint;

            _logger.LogDebug("Retrieved certificate thumbprint: {Thumbprint}", thumbprint);
            return thumbprint;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading certificate thumbprint");
            return null;
        }
    }

    public void StoreCertificate(string certificate, string privateKey)
    {
        try
        {
            File.WriteAllText(_certificatePath, certificate);
            File.WriteAllText(_privateKeyPath, privateKey);

            _logger.LogInformation("Certificate stored successfully at {Path}", _certificatePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing certificate");
            throw;
        }
    }

    public bool HasValidCertificate()
    {
        try
        {
            if (!File.Exists(_certificatePath) || !File.Exists(_privateKeyPath))
            {
                _logger.LogDebug("Certificate or private key file not found");
                return false;
            }

            var certContent = File.ReadAllText(_certificatePath);
            var cert = X509Certificate2.CreateFromPem(certContent);

            // Check if certificate is expired
            if (cert.NotAfter < DateTime.Now)
            {
                _logger.LogWarning("Certificate has expired: {ExpiryDate}", cert.NotAfter);
                return false;
            }

            // Check if certificate will expire soon (within 7 days)
            if (cert.NotAfter < DateTime.Now.AddDays(7))
            {
                _logger.LogWarning("Certificate will expire soon: {ExpiryDate}", cert.NotAfter);
            }

            _logger.LogDebug("Certificate is valid until {ExpiryDate}", cert.NotAfter);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating certificate");
            return false;
        }
    }
}
