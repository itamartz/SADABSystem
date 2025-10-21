namespace SADAB.Agent.Configuration;

public class AgentConfiguration
{
    public string ServerUrl { get; set; } = "https://localhost:5001";
    public Guid? AgentId { get; set; }
    public string? CertificatePath { get; set; }
    public string? PrivateKeyPath { get; set; }
    public string? CertificateThumbprint { get; set; }
    public DateTime? CertificateExpiresAt { get; set; }
    public int HeartbeatIntervalSeconds { get; set; } = 30;
    public int DeploymentCheckIntervalSeconds { get; set; } = 60;
    public int CommandCheckIntervalSeconds { get; set; } = 30;
    public int InventoryCollectionIntervalMinutes { get; set; } = 60;
    public string WorkingDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "SADAB", "Agent");

    public override string ToString()
    {
        return $"ServerUrl={ServerUrl}, AgentId={AgentId?.ToString() ?? "null"}, " +
               $"CertificatePath={CertificatePath ?? "null"}, PrivateKeyPath={PrivateKeyPath ?? "null"}, " +
               $"CertificateThumbprint={CertificateThumbprint ?? "null"}, " +
               $"CertificateExpiresAt={CertificateExpiresAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "null"}, " +
               $"HeartbeatIntervalSeconds={HeartbeatIntervalSeconds}, DeploymentCheckIntervalSeconds={DeploymentCheckIntervalSeconds}, " +
               $"CommandCheckIntervalSeconds={CommandCheckIntervalSeconds}, InventoryCollectionIntervalMinutes={InventoryCollectionIntervalMinutes}, " +
               $"WorkingDirectory={WorkingDirectory}";
    }
}
