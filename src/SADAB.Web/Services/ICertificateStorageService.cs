namespace SADAB.Web.Services;

/// <summary>
/// Service for managing the web application's client certificate for API authentication
/// </summary>
public interface ICertificateStorageService
{
    /// <summary>
    /// Gets the stored certificate thumbprint
    /// </summary>
    /// <returns>Certificate thumbprint or null if not found</returns>
    string? GetCertificateThumbprint();

    /// <summary>
    /// Stores the certificate information received from API registration
    /// </summary>
    /// <param name="certificate">Certificate content in PEM format</param>
    /// <param name="privateKey">Private key content in PEM format</param>
    void StoreCertificate(string certificate, string privateKey);

    /// <summary>
    /// Checks if a valid certificate exists
    /// </summary>
    /// <returns>True if certificate exists and is valid</returns>
    bool HasValidCertificate();
}
