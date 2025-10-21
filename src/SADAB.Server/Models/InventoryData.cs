namespace SADAB.Server.Models;

public class InventoryData
{
    public Guid Id { get; set; }
    public Guid AgentId { get; set; }
    public string HardwareInfo { get; set; } = "{}"; // JSON
    public string InstalledSoftware { get; set; } = "[]"; // JSON
    public string EnvironmentVariables { get; set; } = "{}"; // JSON
    public string RunningServices { get; set; } = "[]"; // JSON
    public DateTime CollectedAt { get; set; }

    // Navigation properties
    public Agent Agent { get; set; } = null!;
}
