using Microsoft.Extensions.Configuration;
using System.Text.Json;

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
    public int InventoryCollectionIntervalMinutes { get; set; } = 5;
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

    public async Task LoadDefaultsAsync1()
    {
        // Load agent configuration
        //var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"SADAB", "Agent", "config.json");
        var configPath = Path.Combine(this.WorkingDirectory, "config.json");

        if (File.Exists(configPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(configPath);
                var loadedConfig = JsonSerializer.Deserialize<AgentConfiguration>(json);
                if (loadedConfig != null)
                {
                    //agentConfig = loadedConfig;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading configuration: {ex.Message}");
            }
        }
    }

    public async Task SaveConfigurationAsync()
    {
        try
        {
            var configPath = Path.Combine(this.WorkingDirectory, "config.json");

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(configPath, json);
        }
        catch (Exception ex)
        {
            throw new Exception("Error saving configuration", ex);
        }
    }
}
