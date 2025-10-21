using SADAB.Shared.Enums;

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
}
