using SADAB.Shared.Enums;

namespace SADAB.Server.Models;

public class CommandExecution
{
    public Guid Id { get; set; }
    public Guid AgentId { get; set; }
    public required string Command { get; set; }
    public string? Arguments { get; set; }
    public bool RunAsAdmin { get; set; }
    public int TimeoutMinutes { get; set; }
    public CommandExecutionStatus Status { get; set; }
    public DateTime RequestedAt { get; set; }
    public string? RequestedBy { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? ExitCode { get; set; }
    public string? Output { get; set; }
    public string? ErrorOutput { get; set; }

    // Navigation properties
    public Agent Agent { get; set; } = null!;

    public override string ToString()
    {
        return $"Id={Id}, AgentId={AgentId}, Command={Command}, Arguments={Arguments ?? "null"}, " +
               $"RunAsAdmin={RunAsAdmin}, TimeoutMinutes={TimeoutMinutes}, Status={Status}, " +
               $"RequestedAt={RequestedAt:yyyy-MM-dd HH:mm:ss}, RequestedBy={RequestedBy ?? "null"}, " +
               $"StartedAt={StartedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "null"}, " +
               $"CompletedAt={CompletedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "null"}, " +
               $"ExitCode={ExitCode?.ToString() ?? "null"}";
    }
}
