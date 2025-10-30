using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using SADAB.API.Data;
using SADAB.API.Models;
using Microsoft.EntityFrameworkCore;

namespace SADAB.API.Services;

public interface ICertificateService
{
    Task<(string certificate, string privateKey, DateTime expiresAt)> GenerateCertificateAsync(Guid agentId, string machineName, int validityDays = 60);
    Task<bool> ValidateCertificateAsync(string thumbprint);
    Task RevokeCertificateAsync(string thumbprint, string reason);
    Task<AgentCertificate?> GetCertificateByThumbprintAsync(string thumbprint);
}

public class CertificateService : ICertificateService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CertificateService> _logger;
    private readonly int _keySize;
    private readonly string _organization;
    private readonly string _organizationalUnit;
    private readonly string _clientAuthenticationOid;
    private readonly string _certBeginMarker;
    private readonly string _certEndMarker;
    private readonly string _keyBeginMarker;
    private readonly string _keyEndMarker;
    private readonly int _pemLineLength;

    public CertificateService(
        ApplicationDbContext context,
        IConfiguration configuration,
        ILogger<CertificateService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;

        _keySize = _configuration.GetValue<int>("CertificateSettings:KeySize");
        _organization = _configuration["CertificateSettings:Organization"] ?? "SADAB";
        _organizationalUnit = _configuration["CertificateSettings:OrganizationalUnit"] ?? "Agent";
        _clientAuthenticationOid = "1.3.6.1.5.5.7.3.2";
        _certBeginMarker = "-----BEGIN CERTIFICATE-----";
        _certEndMarker = "-----END CERTIFICATE-----";
        _keyBeginMarker = "-----BEGIN RSA PRIVATE KEY-----";
        _keyEndMarker = "-----END RSA PRIVATE KEY-----";
        _pemLineLength = 64;
    }

    public async Task<(string certificate, string privateKey, DateTime expiresAt)> GenerateCertificateAsync(
        Guid agentId, string machineName, int validityDays = 60)
    {
        try
        {
            // Use configured validity days if default is provided
            if (validityDays == 60)
            {
                validityDays = _configuration.GetValue<int>("CertificateSettings:ValidityDays");
            }

            // Generate RSA key pair
            using var rsa = RSA.Create(_keySize);

            // Create certificate request
            var subject = new X500DistinguishedName($"CN={machineName},O={_organization},OU={_organizationalUnit}-{agentId}");
            var request = new CertificateRequest(
                subject,
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            // Add extensions
            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                    critical: true));

            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    new OidCollection { new Oid(_clientAuthenticationOid) },
                    critical: true));

            request.CertificateExtensions.Add(
                new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false));

            // Generate self-signed certificate
            var notBefore = DateTimeOffset.UtcNow;
            var notAfter = notBefore.AddDays(validityDays);

            var certificate = request.CreateSelfSigned(notBefore, notAfter);

            // Export certificate and private key
            var certPem = ExportCertificateToPem(certificate);
            var keyPem = ExportPrivateKeyToPem(rsa);
            var thumbprint = certificate.Thumbprint;

            // Store in database
            var agentCertificate = new AgentCertificate
            {
                Id = Guid.NewGuid(),
                AgentId = agentId,
                Thumbprint = thumbprint,
                CertificateData = certPem,
                IssuedAt = DateTime.Now,
                ExpiresAt = notAfter.UtcDateTime,
                IsRevoked = false
            };

            _context.AgentCertificates.Add(agentCertificate);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Generated certificate for agent {AgentId} MachineName {MachineName} with thumbprint {Thumbprint}", agentId, machineName, thumbprint);

            return (certPem, keyPem, notAfter.UtcDateTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating certificate for agent {AgentId}", agentId);
            throw;
        }
    }

    public async Task<bool> ValidateCertificateAsync(string thumbprint)
    {
        _logger.LogDebug("Validating certificate with thumbprint {Thumbprint}", thumbprint);
        var cert = await _context.AgentCertificates
            .FirstOrDefaultAsync(c => c.Thumbprint == thumbprint);

        if (cert == null)
        {
            _logger.LogWarning("Certificate with thumbprint {Thumbprint} not found", thumbprint);
            return false;
        }

        if (cert.IsRevoked)
        {
            _logger.LogWarning("Certificate {Thumbprint} is revoked", thumbprint);
            return false;
        }

        if (cert.ExpiresAt < DateTime.Now)
        {
            _logger.LogWarning("Certificate {Thumbprint} is expired", thumbprint);
            return false;
        }
        _logger.LogDebug("Certificate {Thumbprint} is valid", thumbprint);
        _logger.LogDebug("Certificate details: {@cert}", cert);
        return true;
    }

    public async Task RevokeCertificateAsync(string thumbprint, string reason)
    {
        var cert = await _context.AgentCertificates
            .FirstOrDefaultAsync(c => c.Thumbprint == thumbprint);

        if (cert == null)
        {
            throw new InvalidOperationException($"Certificate {thumbprint} not found");
        }

        cert.IsRevoked = true;
        cert.RevokedAt = DateTime.Now;
        cert.RevocationReason = reason;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Revoked certificate {Thumbprint}: {Reason}", thumbprint, reason);
    }

    public async Task<AgentCertificate?> GetCertificateByThumbprintAsync(string thumbprint)
    {
        return await _context.AgentCertificates
            .Include(c => c.Agent)
            .FirstOrDefaultAsync(c => c.Thumbprint == thumbprint);
    }

    private string ExportCertificateToPem(X509Certificate2 certificate)
    {
        var certBytes = certificate.Export(X509ContentType.Cert);
        var certBase64 = Convert.ToBase64String(certBytes);

        var sb = new StringBuilder();
        sb.AppendLine(_certBeginMarker);

        for (int i = 0; i < certBase64.Length; i += _pemLineLength)
        {
            var length = Math.Min(_pemLineLength, certBase64.Length - i);
            sb.AppendLine(certBase64.Substring(i, length));
        }

        sb.AppendLine(_certEndMarker);
        return sb.ToString();
    }

    private string ExportPrivateKeyToPem(RSA rsa)
    {
        var keyBytes = rsa.ExportRSAPrivateKey();
        var keyBase64 = Convert.ToBase64String(keyBytes);

        var sb = new StringBuilder();
        sb.AppendLine(_keyBeginMarker);

        for (int i = 0; i < keyBase64.Length; i += _pemLineLength)
        {
            var length = Math.Min(_pemLineLength, keyBase64.Length - i);
            sb.AppendLine(keyBase64.Substring(i, length));
        }

        sb.AppendLine(_keyEndMarker);
        return sb.ToString();
    }
}
