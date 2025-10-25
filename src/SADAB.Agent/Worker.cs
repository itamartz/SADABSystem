using SADAB.Agent.Configuration;
using SADAB.Agent.Services;
using SADAB.Shared.DTOs;
using SADAB.Shared.Enums;
using System.Net.NetworkInformation;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace SADAB.Agent;

public class Worker : BackgroundService
{
    private readonly AgentConfiguration _configuration;
    private readonly IConfiguration _appConfiguration;
    private readonly IApiClientService _apiClient;
    private readonly IDeploymentExecutorService _deploymentExecutor;
    private readonly ICommandExecutorService _commandExecutor;
    private readonly IInventoryCollectorService _inventoryCollector;
    private readonly ILogger<Worker> _logger;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly string _certificateFileName;
    private readonly string _privateKeyFileName;
    private readonly string _configFileName;
    private readonly int _certificateRefreshThresholdDays;
    private readonly int _certificateRefreshCheckIntervalMinutes;

    public Worker(
        AgentConfiguration configuration,
        IConfiguration appConfiguration,
        IApiClientService apiClient,
        IDeploymentExecutorService deploymentExecutor,
        ICommandExecutorService commandExecutor,
        IInventoryCollectorService inventoryCollector,
        ILogger<Worker> logger,
        IHostApplicationLifetime lifetime)
    {
        _configuration = configuration;
        _appConfiguration = appConfiguration;
        _apiClient = apiClient;
        _deploymentExecutor = deploymentExecutor;
        _commandExecutor = commandExecutor;
        _inventoryCollector = inventoryCollector;
        _logger = logger;
        _lifetime = lifetime;

        _certificateFileName = _appConfiguration["AgentSettings:CertificateFileName"] ?? "agent.crt";
        _privateKeyFileName = _appConfiguration["AgentSettings:PrivateKeyFileName"] ?? "agent.key";
        _configFileName = _appConfiguration["AgentSettings:ConfigFileName"] ?? "config.json";
        _certificateRefreshThresholdDays = _appConfiguration.GetValue<int>("AgentSettings:CertificateRefreshThresholdDays");
        _certificateRefreshCheckIntervalMinutes = _appConfiguration.GetValue<int>("AgentSettings:CertificateRefreshCheckIntervalMinutes");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation(_appConfiguration["Messages:StartingAgent"] ?? "SADAB Agent starting...");

            // Ensure working directory exists
            _logger.LogDebug("Ensuring working directory exists at {WorkingDirectory}", _configuration.WorkingDirectory);
            Directory.CreateDirectory(_configuration.WorkingDirectory);

            // Register or load agent configuration
            if (!_configuration.AgentId.HasValue || string.IsNullOrEmpty(_configuration.CertificateThumbprint))
            {
                _logger.LogInformation("Agent not registered. Initiating registration process.");
                await RegisterAgentAsync();
            }

            if (!_configuration.AgentId.HasValue)
            {
                _logger.LogError(_appConfiguration["Messages:RegistrationFailed"] ?? "Failed to register agent. Stopping service.");
                _lifetime.StopApplication();
                return;
            }

            var startedMessage = _appConfiguration["Messages:AgentStarted"] ?? "SADAB Agent started with ID: {0}";
            _logger.LogInformation(startedMessage, _configuration.AgentId);

            // Start background tasks

            var realodedTask = RealodedAgentConfigurationAsync(stoppingToken);

            var heartbeatTask = HeartbeatLoopAsync(stoppingToken);

            //var deploymentTask = DeploymentCheckLoopAsync(stoppingToken);
            //var commandTask = CommandCheckLoopAsync(stoppingToken);
            var inventoryTask = InventoryCollectionLoopAsync(stoppingToken);
            //var certificateTask = CertificateRefreshLoopAsync(stoppingToken);

            // Wait for all tasks
            //await Task.WhenAll(heartbeatTask, deploymentTask, commandTask, inventoryTask, certificateTask);
            //await Task.WhenAll(heartbeatTask, certificateTask);
            await Task.WhenAll(inventoryTask, heartbeatTask);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in agent");
            _lifetime.StopApplication();
        }
    }

    private async Task RealodedAgentConfigurationAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var configPath = Path.Combine(_configuration.WorkingDirectory, _configFileName);
                if (File.Exists(configPath))
                {
                    var json = await File.ReadAllTextAsync(configPath, stoppingToken);
                    var updatedConfig = JsonSerializer.Deserialize<AgentConfiguration>(json);
                    if (updatedConfig != null)
                    {
                        _configuration.AgentId = updatedConfig.AgentId;
                        _configuration.CertificateThumbprint = updatedConfig.CertificateThumbprint;
                        _configuration.CertificateExpiresAt = updatedConfig.CertificateExpiresAt;
                        _configuration.HeartbeatIntervalSeconds = updatedConfig.HeartbeatIntervalSeconds;
                        _configuration.DeploymentCheckIntervalSeconds = updatedConfig.DeploymentCheckIntervalSeconds;
                        _configuration.CommandCheckIntervalSeconds = updatedConfig.CommandCheckIntervalSeconds;
                        _configuration.InventoryCollectionIntervalMinutes = updatedConfig.InventoryCollectionIntervalMinutes;
                        _logger.LogInformation("Agent configuration reloaded successfully");
                        _logger.LogDebug("Updated configuration: {@Configuration}", _configuration);
                    }
                }
                else
                {
                    _logger.LogWarning("Configuration file not found at {ConfigPath}", configPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reloading agent configuration");
            }
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        }
    }

    private async Task RegisterAgentAsync()
    {
        try
        {
            _logger.LogInformation("Registering agent with server...");

            var machineId = GetMachineId();
            var machineName = Environment.MachineName;
            var os = Environment.OSVersion.ToString();
            var ipAddress = GetLocalIPAddress();

            var request = new AgentRegistrationRequest
            {
                MachineName = machineName,
                MachineId = machineId,
                OperatingSystem = os,
                IpAddress = ipAddress,
                Metadata = new Dictionary<string, string>
                {
                    ["UserName"] = Environment.UserName,
                    ["ProcessorCount"] = Environment.ProcessorCount.ToString(),
                    ["Is64Bit"] = Environment.Is64BitOperatingSystem.ToString()
                }
            };

            var response = await _apiClient.RegisterAsync(request);

            if (response != null)
            {
                _configuration.AgentId = response.AgentId;
                _configuration.CertificateExpiresAt = response.ExpiresAt;

                // Save certificate and private key
                var certPath = Path.Combine(_configuration.WorkingDirectory, _certificateFileName);
                var keyPath = Path.Combine(_configuration.WorkingDirectory, _privateKeyFileName);

                await File.WriteAllTextAsync(certPath, response.Certificate);
                await File.WriteAllTextAsync(keyPath, response.PrivateKey);

                _configuration.CertificatePath = certPath;
                _configuration.PrivateKeyPath = keyPath;

                // Extract thumbprint (this is simplified - in production, parse the cert properly)
                _configuration.CertificateThumbprint = ExtractThumbprint(response.Certificate);

                // Save configuration
                await SaveConfigurationAsync();

                _logger.LogInformation("Agent registered successfully with ID: {AgentId}", response.AgentId);
            }
            else
            {
                _logger.LogError("Failed to register agent");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering agent");
        }
    }

    private async Task HeartbeatLoopAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogDebug("about to sending heartbeat to server...");
                var request = new AgentHeartbeatRequest
                {
                    Status = AgentStatus.Online,
                    IpAddress = GetLocalIPAddress(),
                    SystemInfo = new Dictionary<string, object>
                    {
                        ["MachineName"] = Environment.MachineName,
                        ["UserName"] = Environment.UserName,
                        ["OSVersion"] = Environment.OSVersion.ToString()
                    }
                };

                _logger.LogDebug("Heartbeat request: {request}", request);
                await _apiClient.SendHeartbeatAsync(request);
                _logger.LogDebug(_appConfiguration["Messages:HeartbeatSent"] ?? "Heartbeat sent");
                _logger.LogInformation("Heartbeat task finished");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending heartbeat");
            }

            var nextCheckTime = DateTime.Now.AddSeconds(_configuration.CommandCheckIntervalSeconds);
            _logger.LogDebug("Waiting {HeartbeatIntervalSeconds} seconds before next heartbeat, Next check at {nextCheckTime}", _configuration.HeartbeatIntervalSeconds, nextCheckTime);
            _logger.LogInformation("Heartbeat task finished");
            await Task.Delay(TimeSpan.FromSeconds(_configuration.HeartbeatIntervalSeconds), stoppingToken);
        }
    }

    private async Task DeploymentCheckLoopAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var deployments = await _apiClient.GetPendingDeploymentsAsync();

                foreach (var deployment in deployments)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _deploymentExecutor.ExecuteDeploymentAsync(deployment);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error executing deployment {DeploymentId}", deployment.DeploymentId);
                        }
                    }, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for deployments");
            }

            await Task.Delay(TimeSpan.FromSeconds(_configuration.DeploymentCheckIntervalSeconds), stoppingToken);
        }
    }

    private async Task CommandCheckLoopAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogDebug("Checking for pending commands...");
                var commands = await _apiClient.GetPendingCommandsAsync();
                _logger.LogDebug("Found {CommandCount} pending commands", commands.Count);

                foreach (var command in commands)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            _logger.LogDebug("About to executing command {CommandId}: {Command} {Arguments}", command.Id, command.Command, command.Arguments);
                            await _commandExecutor.ExecuteCommandAsync(command);
                            _logger.LogDebug("Finished executing command {CommandId}", command.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error executing command {CommandId}", command.Id);
                        }
                    }, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for commands");
            }

            var nextCheckTime = DateTime.Now.AddSeconds(_configuration.CommandCheckIntervalSeconds);
            _logger.LogDebug("Waiting {CommandCheckIntervalSeconds} seconds before next command check. Next check at {nextCheckTime}", _configuration.CommandCheckIntervalSeconds, nextCheckTime);
            _logger.LogInformation("Command task finished");

            await Task.Delay(TimeSpan.FromSeconds(_configuration.CommandCheckIntervalSeconds), stoppingToken);
        }
    }

    private async Task InventoryCollectionLoopAsync(CancellationToken stoppingToken)
    {
        // Initial delay before first collection
        //await Task.Delay(TimeSpan.FromMinutes(_inventoryInitialDelayMinutes), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var inventory = await _inventoryCollector.CollectInventoryAsync();
                await _apiClient.SubmitInventoryAsync(inventory);

                _logger.LogInformation(_appConfiguration["Messages:InventorySubmitted"] ?? "Inventory data submitted");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting/submitting inventory");
            }

            // Check based on configured interval
            var nextCheckTime = DateTime.Now.AddMinutes(_configuration.InventoryCollectionIntervalMinutes);
            _logger.LogDebug("Waiting {inventoryCollectionIntervalMinute} minutes before next inventory check. Next check at {extCheckTime}", _configuration.InventoryCollectionIntervalMinutes, nextCheckTime);

            await Task.Delay(TimeSpan.FromMinutes(_configuration.InventoryCollectionIntervalMinutes), stoppingToken);




        }
    }

    private async Task CertificateRefreshLoopAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_configuration.CertificateExpiresAt.HasValue)
                {
                    var daysUntilExpiry = (_configuration.CertificateExpiresAt.Value - DateTime.UtcNow).TotalDays;

                    // Refresh certificate based on configured threshold
                    if (daysUntilExpiry <= _certificateRefreshThresholdDays)
                    {
                        var expiringMessage = _appConfiguration["Messages:CertificateExpiring"] ?? "Certificate expires in {0} days, refreshing...";
                        _logger.LogInformation(expiringMessage, daysUntilExpiry);

                        var request = new CertificateRefreshRequest
                        {
                            AgentId = _configuration.AgentId!.Value,
                            CurrentCertificateThumbprint = _configuration.CertificateThumbprint!
                        };

                        _logger.LogDebug("Sending certificate refresh request to server {request}", request);
                        var response = await _apiClient.RefreshCertificateAsync(request);

                        if (response != null)
                        {
                            _configuration.CertificateExpiresAt = response.ExpiresAt;

                            // Save new certificate and private key
                            var certPath = Path.Combine(_configuration.WorkingDirectory, _certificateFileName);
                            _logger.LogInformation("certPath is {certPath}", certPath);

                            var keyPath = Path.Combine(_configuration.WorkingDirectory, _privateKeyFileName);
                            _logger.LogInformation("keyPath is {keyPath}", keyPath);

                            _logger.LogDebug("Writing new certificate to {certPath} and key to {keyPath}", certPath, keyPath);
                            await File.WriteAllTextAsync(certPath, response.Certificate);
                            _logger.LogDebug("Wrote new certificate to {certPath}", certPath);

                            _logger.LogDebug("Writing new private key to {keyPath}", keyPath);
                            await File.WriteAllTextAsync(keyPath, response.PrivateKey);
                            _logger.LogDebug("Wrote new private key to {keyPath}", keyPath);

                            _logger.LogDebug("Updating configuration with new certificate thumbprint");
                            _configuration.CertificateThumbprint = ExtractThumbprint(response.Certificate);

                            _logger.LogDebug("Saving updated configuration");
                            await SaveConfigurationAsync();
                            _logger.LogDebug("Configuration was updated {_configuration}", _configuration);
                            _logger.LogInformation(_appConfiguration["Messages:CertificateRefreshed"] ?? "Certificate refreshed successfully");
                        }
                    }
                    else
                    {
                        _logger.LogDebug("Certificate valid for {daysUntilExpiry} more days, no refresh needed", Convert.ToInt32(daysUntilExpiry));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing certificate");
            }

            // Check based on configured interval
            var nextCheckTime = DateTime.Now.AddMinutes(_certificateRefreshCheckIntervalMinutes);
            _logger.LogDebug("Waiting {_certificateRefreshCheckIntervalMinutes} minutes before next certificate check. Next check at {nextCheckTime}", _certificateRefreshCheckIntervalMinutes, nextCheckTime);
            await Task.Delay(TimeSpan.FromMinutes(_certificateRefreshCheckIntervalMinutes), stoppingToken);
        }
    }

    private string GetMachineId()
    {
        // Try to get a unique machine identifier
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var mc = new System.Management.ManagementClass("Win32_ComputerSystemProduct");
                foreach (System.Management.ManagementObject mo in mc.GetInstances())
                {
                    return mo["UUID"]?.ToString() ?? Guid.NewGuid().ToString();
                }
            }
            catch
            {
                // Fallback to MAC address
            }
        }

        // Fallback to MAC address
        var mac = NetworkInterface.GetAllNetworkInterfaces()
            .Where(nic => nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Select(nic => nic.GetPhysicalAddress().ToString())
            .FirstOrDefault();

        return mac ?? Guid.NewGuid().ToString();
    }

    private string GetLocalIPAddress()
    {
        try
        {
            var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
            var ip = host.AddressList.FirstOrDefault(addr =>
                addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            return ip?.ToString() ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    private string ExtractThumbprint(string certificatePem)
    {
        try
        {
            // Remove PEM headers
            var base64 = certificatePem
                .Replace("-----BEGIN CERTIFICATE-----", "")
                .Replace("-----END CERTIFICATE-----", "")
                .Replace("\n", "")
                .Replace("\r", "");

            var certBytes = Convert.FromBase64String(base64);

            using var cert = new System.Security.Cryptography.X509Certificates.X509Certificate2(certBytes);
            return cert.Thumbprint;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting certificate thumbprint");
            return Guid.NewGuid().ToString("N");
        }
    }

    private async Task SaveConfigurationAsync()
    {
        try
        {
            _logger.LogInformation("Saving agent configuration...");
            await _configuration.SaveConfigurationAsync();
            _logger.LogInformation("Agent configuration saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving configuration");
        }
    }
}
