using SADAB.Shared.Enums;

namespace SADAB.Shared.DTOs;

public class AgentRegistrationRequest
{
    public required string MachineName { get; set; }
    public required string MachineId { get; set; }
    public required string OperatingSystem { get; set; }
    public string? IpAddress { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

public class AgentRegistrationResponse
{
    public Guid AgentId { get; set; }
    public required string Certificate { get; set; }
    public required string PrivateKey { get; set; }
    public DateTime ExpiresAt { get; set; }
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
}

public class AgentHeartbeatRequest
{
    public AgentStatus Status { get; set; }
    public string? IpAddress { get; set; }
    public Dictionary<string, object>? SystemInfo { get; set; }
}
