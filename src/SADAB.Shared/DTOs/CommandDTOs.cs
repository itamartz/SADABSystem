using SADAB.Shared.Enums;

namespace SADAB.Shared.DTOs;

public class ExecuteCommandRequest
{
    public required string Command { get; set; }
    public string? Arguments { get; set; }
    public List<Guid>? TargetAgentIds { get; set; }
    public bool RunAsAdmin { get; set; } = false;
    public int TimeoutMinutes { get; set; } = 5;
}

public class CommandExecutionDto
{
    public Guid Id { get; set; }
    public Guid AgentId { get; set; }
    public string? AgentName { get; set; }
    public required string Command { get; set; }
    public string? Arguments { get; set; }
    public CommandExecutionStatus Status { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? ExitCode { get; set; }
    public string? Output { get; set; }
    public string? ErrorOutput { get; set; }
}
