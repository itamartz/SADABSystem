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

    public override string ToString()
    {
        return $"Id={Id}, AgentId={AgentId}, CollectedAt={CollectedAt:yyyy-MM-dd HH:mm:ss}, " +
               $"HardwareInfo={(HardwareInfo.Length > 50 ? HardwareInfo.Substring(0, 50) + "..." : HardwareInfo)}, " +
               $"InstalledSoftware={(InstalledSoftware.Length > 50 ? InstalledSoftware.Substring(0, 50) + "..." : InstalledSoftware)}";
    }
}
