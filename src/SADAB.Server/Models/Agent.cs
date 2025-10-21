using SADAB.Shared.Enums;

namespace SADAB.Server.Models;

public class Agent
{
    public Guid Id { get; set; }
    public required string MachineName { get; set; }
    public required string MachineId { get; set; }
    public required string OperatingSystem { get; set; }
    public string? IpAddress { get; set; }
    public AgentStatus Status { get; set; }
    public DateTime LastHeartbeat { get; set; }
    public DateTime RegisteredAt { get; set; }
    public string? CurrentCertificateThumbprint { get; set; }
    public DateTime? CertificateExpiresAt { get; set; }
    public string? Metadata { get; set; } // JSON serialized

    // Navigation properties
    public ICollection<AgentCertificate> Certificates { get; set; } = new List<AgentCertificate>();
    public ICollection<DeploymentResult> DeploymentResults { get; set; } = new List<DeploymentResult>();
    public ICollection<InventoryData> InventoryData { get; set; } = new List<InventoryData>();
    public ICollection<CommandExecution> CommandExecutions { get; set; } = new List<CommandExecution>();
}
