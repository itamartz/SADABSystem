namespace SADAB.Server.Models;

public class AgentCertificate
{
    public Guid Id { get; set; }
    public Guid AgentId { get; set; }
    public required string Thumbprint { get; set; }
    public required string CertificateData { get; set; } // PEM format
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevocationReason { get; set; }

    // Navigation properties
    public Agent Agent { get; set; } = null!;
}
