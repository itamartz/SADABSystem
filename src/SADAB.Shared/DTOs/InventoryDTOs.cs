namespace SADAB.Shared.DTOs;

public class InventoryDataDto
{
    public Guid AgentId { get; set; }
    public Dictionary<string, object> HardwareInfo { get; set; } = new();
    public List<InstalledSoftwareDto> InstalledSoftware { get; set; } = new();
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
    public List<string> RunningServices { get; set; } = new();
    public DateTime CollectedAt { get; set; }

    public override string ToString()
    {
        return $"AgentId={AgentId}, HardwareInfo={HardwareInfo.Count} items, " +
               $"InstalledSoftware={InstalledSoftware.Count} items, EnvironmentVariables={EnvironmentVariables.Count} items, " +
               $"RunningServices={RunningServices.Count} items, CollectedAt={CollectedAt:yyyy-MM-dd HH:mm:ss}";
    }
}

public class InstalledSoftwareDto
{
    public required string Name { get; set; }
    public string? Version { get; set; }
    public string? Publisher { get; set; }
    public DateTime? InstallDate { get; set; }

    public override string ToString()
    {
        return $"Name={Name}, Version={Version ?? "null"}, Publisher={Publisher ?? "null"}, " +
               $"InstallDate={InstallDate?.ToString("yyyy-MM-dd") ?? "null"}";
    }
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

    public override string ToString()
    {
        return $"Processor={Processor ?? "null"}, TotalMemoryMB={TotalMemoryMB?.ToString() ?? "null"}, " +
               $"FreeMemoryMB={FreeMemoryMB?.ToString() ?? "null"}, Disks={Disks.Count} disks, " +
               $"BiosVersion={BiosVersion ?? "null"}, Manufacturer={Manufacturer ?? "null"}, Model={Model ?? "null"}";
    }
}

public class DiskInfoDto
{
    public required string Name { get; set; }
    public long? TotalSizeGB { get; set; }
    public long? FreeSizeGB { get; set; }
    public string? FileSystem { get; set; }

    public override string ToString()
    {
        return $"Name={Name}, TotalSizeGB={TotalSizeGB?.ToString() ?? "null"}, " +
               $"FreeSizeGB={FreeSizeGB?.ToString() ?? "null"}, FileSystem={FileSystem ?? "null"}";
    }
}
