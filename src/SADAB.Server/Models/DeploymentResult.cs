using SADAB.Shared.Enums;

namespace SADAB.Server.Models;

public class DeploymentResult
{
    public Guid Id { get; set; }
    public Guid DeploymentId { get; set; }
    public Guid AgentId { get; set; }
    public DeploymentStatus Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? ExitCode { get; set; }
    public string? Output { get; set; }
    public string? ErrorMessage { get; set; }

    // Navigation properties
    public Deployment Deployment { get; set; } = null!;
    public Agent Agent { get; set; } = null!;

    public override string ToString()
    {
        return $"Id={Id}, DeploymentId={DeploymentId}, AgentId={AgentId}, Status={Status}, " +
               $"StartedAt={StartedAt:yyyy-MM-dd HH:mm:ss}, CompletedAt={CompletedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "null"}, " +
               $"ExitCode={ExitCode?.ToString() ?? "null"}, " +
               $"Output={(Output != null ? $"\"{Output.Substring(0, Math.Min(50, Output.Length))}...\"" : "null")}, " +
               $"ErrorMessage={ErrorMessage ?? "null"}";
    }
}
