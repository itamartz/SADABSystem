using SADAB.Shared.Enums;
using System.Text.Json;

namespace SADAB.API.Models;

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

    /// <summary>
    /// JSON string containing list of exit codes that should be considered successful.
    /// Default: "[0]". Common codes: 0 (success), 3010 (success with reboot required), 1641 (reboot initiated)
    /// </summary>
    public string SuccessExitCodesJson { get; set; } = "[0]";

    /// <summary>
    /// List of exit codes that should be considered successful. Not mapped to database.
    /// </summary>
    public List<int> SuccessExitCodes
    {
        get => string.IsNullOrWhiteSpace(SuccessExitCodesJson)
            ? new List<int> { 0 }
            : JsonSerializer.Deserialize<List<int>>(SuccessExitCodesJson) ?? new List<int> { 0 };
        set => SuccessExitCodesJson = JsonSerializer.Serialize(value);
    }

    // Navigation properties
    public ICollection<DeploymentResult> Results { get; set; } = new List<DeploymentResult>();
    public ICollection<DeploymentTarget> Targets { get; set; } = new List<DeploymentTarget>();

    public override string ToString()
    {
        return $"Id={Id}, Name={Name}, Description={Description ?? "null"}, Type={Type}, PackageFolderName={PackageFolderName}, " +
               $"ExecutablePath={ExecutablePath ?? "null"}, Arguments={Arguments ?? "null"}, RunAsAdmin={RunAsAdmin}, " +
               $"TimeoutMinutes={TimeoutMinutes}, Status={Status}, CreatedAt={CreatedAt:yyyy-MM-dd HH:mm:ss}, " +
               $"CreatedBy={CreatedBy ?? "null"}, StartedAt={StartedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "null"}, " +
               $"CompletedAt={CompletedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "null"}";
    }
}
