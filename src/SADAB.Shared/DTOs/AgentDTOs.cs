using SADAB.Shared.Enums;
using SADAB.Shared.Extensions;

namespace SADAB.Shared.DTOs;

public class AgentRegistrationRequest
{
    public required string MachineName { get; set; }
    public required string MachineId { get; set; }
    public required string OperatingSystem { get; set; }
    public string? IpAddress { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>
    /// Returns a string representation with all properties in Key=Value format using reflection.
    /// </summary>
    public override string ToString() => this.ToKeyValueString();
}

public class AgentRegistrationResponse
{
    public Guid AgentId { get; set; }
    public required string Certificate { get; set; }
    public required string PrivateKey { get; set; }
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Returns a string representation with all properties in Key=Value format using reflection.
    /// Sensitive data (PrivateKey) is masked, long strings (Certificate) are truncated.
    /// </summary>
    public override string ToString() => this.ToKeyValueString();
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
    public double? CpuUsagePercent { get; set; }
    public double? MemoryUsagePercent { get; set; }

    /// <summary>
    /// Returns a string representation with all properties in Key=Value format using reflection.
    /// </summary>
    public override string ToString() => this.ToKeyValueString();
}

public class AgentHeartbeatRequest
{
    public AgentStatus Status { get; set; }
    public string? IpAddress { get; set; }
    public Dictionary<string, object>? SystemInfo { get; set; }

    /// <summary>
    /// Returns a string representation with all properties in Key=Value format using reflection.
    /// Automatically includes all properties and expands SystemInfo dictionary.
    /// </summary>
    public override string ToString() => this.ToKeyValueString();
}
