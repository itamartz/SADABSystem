using SADAB.Shared.Extensions;

namespace SADAB.Shared.DTOs;

public class CertificateRefreshRequest
{
    public Guid AgentId { get; set; }
    public required string CurrentCertificateThumbprint { get; set; }

    /// <summary>
    /// Returns a string representation with all properties in Key=Value format using reflection.
    /// </summary>
    public override string ToString() => this.ToKeyValueString();
}

public class CertificateRefreshResponse
{
    public required string Certificate { get; set; }
    public required string PrivateKey { get; set; }
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Returns a string representation with all properties in Key=Value format using reflection.
    /// Sensitive data (PrivateKey) is masked, long strings (Certificate) are truncated.
    /// </summary>
    public override string ToString() => this.ToKeyValueString();
}

public class CreateCertificateRequest
{
    public Guid AgentId { get; set; }
    public int ValidityDays { get; set; } = 60;

    /// <summary>
    /// Returns a string representation with all properties in Key=Value format using reflection.
    /// </summary>
    public override string ToString() => this.ToKeyValueString();
}
