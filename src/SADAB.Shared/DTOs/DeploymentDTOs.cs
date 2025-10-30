using SADAB.Shared.Enums;
using SADAB.Shared.Extensions;

namespace SADAB.Shared.DTOs;

public class CreateDeploymentRequest
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public DeploymentType Type { get; set; }
    public required string PackageFolderName { get; set; }
    public string? ExecutablePath { get; set; }
    public string? Arguments { get; set; }
    public List<Guid>? TargetAgentIds { get; set; }
    public bool RunAsAdmin { get; set; } = true;
    public int TimeoutMinutes { get; set; } = 30;

    /// <summary>
    /// List of exit codes that should be considered successful.
    /// Default: [0]. Common codes: 0 (success), 3010 (success with reboot required), 1641 (reboot initiated)
    /// </summary>
    public List<int> SuccessExitCodes { get; set; } = new() { 0 };

    /// <summary>
    /// Returns a string representation with all properties in Key=Value format using reflection.
    /// </summary>
    public override string ToString() => this.ToKeyValueString();
}

public class DeploymentDto
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public DeploymentType Type { get; set; }
    public required string PackageFolderName { get; set; }
    public DeploymentStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public int TargetAgentCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }

    /// <summary>
    /// Returns a string representation with all properties in Key=Value format using reflection.
    /// </summary>
    public override string ToString() => this.ToKeyValueString();
}

public class DeploymentResultDto
{
    public Guid Id { get; set; }
    public Guid DeploymentId { get; set; }
    public Guid AgentId { get; set; }
    public string? AgentName { get; set; }
    public DeploymentStatus Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? ExitCode { get; set; }
    public string? Output { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Returns a string representation with all properties in Key=Value format using reflection.
    /// Long output strings are truncated.
    /// </summary>
    public override string ToString() => this.ToKeyValueString();
}

public class DeploymentTaskDto
{
    public Guid DeploymentId { get; set; }
    public required string Name { get; set; }
    public DeploymentType Type { get; set; }
    public required List<string> Files { get; set; }
    public string? ExecutablePath { get; set; }
    public string? Arguments { get; set; }
    public bool RunAsAdmin { get; set; }
    public int TimeoutMinutes { get; set; }

    /// <summary>
    /// List of exit codes that should be considered successful.
    /// Default: [0]. Common codes: 0 (success), 3010 (success with reboot required), 1641 (reboot initiated)
    /// </summary>
    public List<int> SuccessExitCodes { get; set; } = new() { 0 };

    /// <summary>
    /// Returns a string representation with all properties in Key=Value format using reflection.
    /// </summary>
    public override string ToString() => this.ToKeyValueString();
}
