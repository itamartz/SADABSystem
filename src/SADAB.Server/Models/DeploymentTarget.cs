namespace SADAB.Server.Models;

public class DeploymentTarget
{
    public Guid Id { get; set; }
    public Guid DeploymentId { get; set; }
    public Guid AgentId { get; set; }
    public DateTime AddedAt { get; set; }

    // Navigation properties
    public Deployment Deployment { get; set; } = null!;
    public Agent Agent { get; set; } = null!;
}
