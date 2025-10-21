using SADAB.Shared.Enums;

namespace SADAB.Shared.DTOs;

public class AgentRegistrationRequest
{
    public required string MachineName { get; set; }
    public required string MachineId { get; set; }
    public required string OperatingSystem { get; set; }
    public string? IpAddress { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }

    public override string ToString()
    {
        return $"MachineName={MachineName}, MachineId={MachineId}, OperatingSystem={OperatingSystem}, " +
               $"IpAddress={IpAddress ?? "null"}, Metadata={Metadata?.Count ?? 0} items";
    }
}

public class AgentRegistrationResponse
{
    public Guid AgentId { get; set; }
    public required string Certificate { get; set; }
    public required string PrivateKey { get; set; }
    public DateTime ExpiresAt { get; set; }

    public override string ToString()
    {
        return $"AgentId={AgentId}, Certificate={Certificate.Substring(0, Math.Min(50, Certificate.Length))}..., " +
               $"PrivateKey=***, ExpiresAt={ExpiresAt:yyyy-MM-dd HH:mm:ss}";
    }
}

public class AgentDto
{
    public Guid Id { get; set; }
    public required string MachineName { get; set; }
    public required string MachineId { get; set; }
    public required string OperatingSystem { get; set; }
    public string? IpAddress { get; set; }
    public AgentStatus Status { get; set; }
    public DateTime LastHeartbeat { get; set; }
    public DateTime RegisteredAt { get; set; }
    public DateTime? CertificateExpiresAt { get; set; }

    public override string ToString()
    {
        return $"Id={Id}, MachineName={MachineName}, MachineId={MachineId}, OperatingSystem={OperatingSystem}, " +
               $"IpAddress={IpAddress ?? "null"}, Status={Status}, LastHeartbeat={LastHeartbeat:yyyy-MM-dd HH:mm:ss}, " +
               $"RegisteredAt={RegisteredAt:yyyy-MM-dd HH:mm:ss}, CertificateExpiresAt={CertificateExpiresAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "null"}";
    }
}

public class AgentHeartbeatRequest
{
    public AgentStatus Status { get; set; }
    public string? IpAddress { get; set; }
    public Dictionary<string, object>? SystemInfo { get; set; }

    public override string ToString()
    {
        return $"Status={Status}, IpAddress={IpAddress ?? "null"}, SystemInfo={SystemInfo?.Count ?? 0} items";
    }
}
