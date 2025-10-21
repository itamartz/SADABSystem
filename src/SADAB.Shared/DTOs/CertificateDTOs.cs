namespace SADAB.Shared.DTOs;

public class CertificateRefreshRequest
{
    public Guid AgentId { get; set; }
    public required string CurrentCertificateThumbprint { get; set; }
}

public class CertificateRefreshResponse
{
    public required string Certificate { get; set; }
    public required string PrivateKey { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public class CreateCertificateRequest
{
    public Guid AgentId { get; set; }
    public int ValidityDays { get; set; } = 60;
}
