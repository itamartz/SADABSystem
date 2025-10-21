namespace SADAB.Shared.DTOs;

public class CertificateRefreshRequest
{
    public Guid AgentId { get; set; }
    public required string CurrentCertificateThumbprint { get; set; }

    public override string ToString()
    {
        return $"AgentId={AgentId}, CurrentCertificateThumbprint={CurrentCertificateThumbprint}";
    }
}

public class CertificateRefreshResponse
{
    public required string Certificate { get; set; }
    public required string PrivateKey { get; set; }
    public DateTime ExpiresAt { get; set; }

    public override string ToString()
    {
        return $"Certificate={Certificate.Substring(0, Math.Min(50, Certificate.Length))}..., PrivateKey=***, ExpiresAt={ExpiresAt:yyyy-MM-dd HH:mm:ss}";
    }
}

public class CreateCertificateRequest
{
    public Guid AgentId { get; set; }
    public int ValidityDays { get; set; } = 60;

    public override string ToString()
    {
        return $"AgentId={AgentId}, ValidityDays={ValidityDays}";
    }
}
