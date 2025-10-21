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

    public override string ToString()
    {
        return $"Id={Id}, AgentId={AgentId}, Thumbprint={Thumbprint}, IssuedAt={IssuedAt:yyyy-MM-dd HH:mm:ss}, " +
               $"ExpiresAt={ExpiresAt:yyyy-MM-dd HH:mm:ss}, IsRevoked={IsRevoked}, " +
               $"RevokedAt={RevokedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "null"}, RevocationReason={RevocationReason ?? "null"}";
    }
}
