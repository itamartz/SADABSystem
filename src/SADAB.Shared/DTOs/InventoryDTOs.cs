namespace SADAB.Shared.DTOs;

public class InventoryDataDto
{
    public Guid AgentId { get; set; }
    public Dictionary<string, object> HardwareInfo { get; set; } = new();
    public List<InstalledSoftwareDto> InstalledSoftware { get; set; } = new();
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
    public List<string> RunningServices { get; set; } = new();
    public DateTime CollectedAt { get; set; }
}

public class InstalledSoftwareDto
{
    public required string Name { get; set; }
    public string? Version { get; set; }
    public string? Publisher { get; set; }
    public DateTime? InstallDate { get; set; }
}

public class HardwareInfoDto
{
    public string? Processor { get; set; }
    public long? TotalMemoryMB { get; set; }
    public long? FreeMemoryMB { get; set; }
    public List<DiskInfoDto> Disks { get; set; } = new();
    public string? BiosVersion { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
}

public class DiskInfoDto
{
    public required string Name { get; set; }
    public long? TotalSizeGB { get; set; }
    public long? FreeSizeGB { get; set; }
    public string? FileSystem { get; set; }
}
