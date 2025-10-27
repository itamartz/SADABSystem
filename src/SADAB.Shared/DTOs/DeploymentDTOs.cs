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

    /// <summary>
    /// List of exit codes that should be considered successful.
    /// Default: [0]. Common codes: 0 (success), 3010 (success with reboot required), 1641 (reboot initiated)
    /// </summary>
    public List<int> SuccessExitCodes { get; set; } = new() { 0 };

    public override string ToString()
    {
        return $"Name={Name}, Description={Description ?? "null"}, Type={Type}, PackageFolderName={PackageFolderName}, " +
               $"ExecutablePath={ExecutablePath ?? "null"}, Arguments={Arguments ?? "null"}, " +
               $"TargetAgentIds={TargetAgentIds?.Count ?? 0} agents, RunAsAdmin={RunAsAdmin}, TimeoutMinutes={TimeoutMinutes}, " +
               $"SuccessExitCodes=[{string.Join(", ", SuccessExitCodes)}]";
    }
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

    public override string ToString()
    {
        return $"Id={Id}, Name={Name}, Description={Description ?? "null"}, Type={Type}, " +
               $"PackageFolderName={PackageFolderName}, Status={Status}, CreatedAt={CreatedAt:yyyy-MM-dd HH:mm:ss}, " +
               $"CreatedBy={CreatedBy ?? "null"}, TargetAgentCount={TargetAgentCount}, " +
               $"SuccessCount={SuccessCount}, FailedCount={FailedCount}";
    }
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

    public override string ToString()
    {
        return $"Id={Id}, DeploymentId={DeploymentId}, AgentId={AgentId}, AgentName={AgentName ?? "null"}, " +
               $"Status={Status}, StartedAt={StartedAt:yyyy-MM-dd HH:mm:ss}, " +
               $"CompletedAt={CompletedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "null"}, ExitCode={ExitCode?.ToString() ?? "null"}, " +
               $"Output={(Output != null ? $"\"{Output.Substring(0, Math.Min(50, Output.Length))}...\"" : "null")}, " +
               $"ErrorMessage={ErrorMessage ?? "null"}";
    }
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

    public override string ToString()
    {
        return $"DeploymentId={DeploymentId}, Name={Name}, Type={Type}, Files={Files.Count} files, " +
               $"ExecutablePath={ExecutablePath ?? "null"}, Arguments={Arguments ?? "null"}, " +
               $"RunAsAdmin={RunAsAdmin}, TimeoutMinutes={TimeoutMinutes}, " +
               $"SuccessExitCodes=[{string.Join(", ", SuccessExitCodes)}]";
    }
}
