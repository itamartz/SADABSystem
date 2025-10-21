using SADAB.Agent.Configuration;
using SADAB.Shared.DTOs;
using System.Management;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace SADAB.Agent.Services;

public interface IInventoryCollectorService
{
    Task<InventoryDataDto> CollectInventoryAsync();
}

[SupportedOSPlatform("windows")]
public class WindowsInventoryCollectorService : IInventoryCollectorService
{
    private readonly AgentConfiguration _configuration;
    private readonly ILogger<WindowsInventoryCollectorService> _logger;

    public WindowsInventoryCollectorService(
        AgentConfiguration configuration,
        ILogger<WindowsInventoryCollectorService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<InventoryDataDto> CollectInventoryAsync()
    {
        _logger.LogInformation("Collecting inventory data");

        var inventory = new InventoryDataDto
        {
            AgentId = _configuration.AgentId!.Value,
            CollectedAt = DateTime.UtcNow
        };

        await Task.Run(() =>
        {
            try
            {
                CollectHardwareInfo(inventory);
                CollectInstalledSoftware(inventory);
                CollectEnvironmentVariables(inventory);
                CollectRunningServices(inventory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting inventory");
            }
        });

        return inventory;
    }

    private void CollectHardwareInfo(InventoryDataDto inventory)
    {
        try
        {
            // Processor
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    inventory.HardwareInfo["Processor"] = obj["Name"]?.ToString() ?? "Unknown";
                    break;
                }
            }

            // Memory
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    var totalMemory = Convert.ToInt64(obj["TotalPhysicalMemory"]);
                    inventory.HardwareInfo["TotalMemoryMB"] = totalMemory / 1024 / 1024;
                    break;
                }
            }

            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    var freeMemory = Convert.ToInt64(obj["FreePhysicalMemory"]);
                    inventory.HardwareInfo["FreeMemoryMB"] = freeMemory / 1024;
                    break;
                }
            }

            // BIOS
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BIOS"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    inventory.HardwareInfo["BiosVersion"] = obj["SMBIOSBIOSVersion"]?.ToString() ?? "Unknown";
                    inventory.HardwareInfo["Manufacturer"] = obj["Manufacturer"]?.ToString() ?? "Unknown";
                    break;
                }
            }

            // Computer System
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    inventory.HardwareInfo["Model"] = obj["Model"]?.ToString() ?? "Unknown";
                    break;
                }
            }

            // Disks
            var disks = new List<object>();
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_LogicalDisk WHERE DriveType=3"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    var size = obj["Size"] != null ? Convert.ToInt64(obj["Size"]) / 1024 / 1024 / 1024 : 0;
                    var freeSpace = obj["FreeSpace"] != null ? Convert.ToInt64(obj["FreeSpace"]) / 1024 / 1024 / 1024 : 0;

                    disks.Add(new
                    {
                        Name = obj["Name"]?.ToString() ?? "Unknown",
                        TotalSizeGB = size,
                        FreeSizeGB = freeSpace,
                        FileSystem = obj["FileSystem"]?.ToString() ?? "Unknown"
                    });
                }
            }
            inventory.HardwareInfo["Disks"] = disks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting hardware info");
        }
    }

    private void CollectInstalledSoftware(InventoryDataDto inventory)
    {
        try
        {
            var software = new List<InstalledSoftwareDto>();

            // Check both 32-bit and 64-bit registry keys
            var registryPaths = new[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            foreach (var path in registryPaths)
            {
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(path);
                    if (key == null) continue;

                    foreach (var subkeyName in key.GetSubKeyNames())
                    {
                        using var subkey = key.OpenSubKey(subkeyName);
                        if (subkey == null) continue;

                        var displayName = subkey.GetValue("DisplayName")?.ToString();
                        if (string.IsNullOrEmpty(displayName)) continue;

                        var version = subkey.GetValue("DisplayVersion")?.ToString();
                        var publisher = subkey.GetValue("Publisher")?.ToString();
                        var installDateStr = subkey.GetValue("InstallDate")?.ToString();

                        DateTime? installDate = null;
                        if (!string.IsNullOrEmpty(installDateStr) && installDateStr.Length == 8)
                        {
                            if (DateTime.TryParseExact(installDateStr, "yyyyMMdd",
                                System.Globalization.CultureInfo.InvariantCulture,
                                System.Globalization.DateTimeStyles.None, out var date))
                            {
                                installDate = date;
                            }
                        }

                        software.Add(new InstalledSoftwareDto
                        {
                            Name = displayName,
                            Version = version,
                            Publisher = publisher,
                            InstallDate = installDate
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error reading registry path {Path}", path);
                }
            }

            inventory.InstalledSoftware = software;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting installed software");
        }
    }

    private void CollectEnvironmentVariables(InventoryDataDto inventory)
    {
        try
        {
            var envVars = Environment.GetEnvironmentVariables();
            foreach (var key in envVars.Keys)
            {
                inventory.EnvironmentVariables[key.ToString()!] = envVars[key]?.ToString() ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting environment variables");
        }
    }

    private void CollectRunningServices(InventoryDataDto inventory)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Service WHERE State='Running'");
            foreach (ManagementObject obj in searcher.Get())
            {
                var serviceName = obj["Name"]?.ToString();
                if (!string.IsNullOrEmpty(serviceName))
                {
                    inventory.RunningServices.Add(serviceName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting running services");
        }
    }
}

// Placeholder for non-Windows systems
public class GenericInventoryCollectorService : IInventoryCollectorService
{
    private readonly AgentConfiguration _configuration;
    private readonly ILogger<GenericInventoryCollectorService> _logger;

    public GenericInventoryCollectorService(
        AgentConfiguration configuration,
        ILogger<GenericInventoryCollectorService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Task<InventoryDataDto> CollectInventoryAsync()
    {
        _logger.LogWarning("Inventory collection not implemented for this platform");

        return Task.FromResult(new InventoryDataDto
        {
            AgentId = _configuration.AgentId!.Value,
            CollectedAt = DateTime.UtcNow
        });
    }
}
