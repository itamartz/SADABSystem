using SADAB.Shared.Enums;

namespace SADAB.Server.Models;

public class Deployment
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public DeploymentType Type { get; set; }
    public required string PackageFolderName { get; set; }
    public string? ExecutablePath { get; set; }
    public string? Arguments { get; set; }
    public bool RunAsAdmin { get; set; }
    public int TimeoutMinutes { get; set; }
    public DeploymentStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Navigation properties
    public ICollection<DeploymentResult> Results { get; set; } = new List<DeploymentResult>();
    public ICollection<DeploymentTarget> Targets { get; set; } = new List<DeploymentTarget>();
}
