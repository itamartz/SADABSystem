using SADAB.Agent.Configuration;
using SADAB.Shared.DTOs;
using SADAB.Shared.Enums;
using System.Diagnostics;
using System.Text;

namespace SADAB.Agent.Services;

public interface IDeploymentExecutorService
{
    Task ExecuteDeploymentAsync(DeploymentTaskDto deployment);
}

public class DeploymentExecutorService : IDeploymentExecutorService
{
    private readonly IApiClientService _apiClient;
    private readonly AgentConfiguration _configuration;
    private readonly ILogger<DeploymentExecutorService> _logger;

    public DeploymentExecutorService(
        IApiClientService apiClient,
        AgentConfiguration configuration,
        ILogger<DeploymentExecutorService> logger)
    {
        _apiClient = apiClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task ExecuteDeploymentAsync(DeploymentTaskDto deployment)
    {
        var deploymentPath = Path.Combine(_configuration.WorkingDirectory, "Deployments", deployment.DeploymentId.ToString());
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("Starting deployment {DeploymentId}: {Name}", deployment.DeploymentId, deployment.Name);

            // Create deployment directory
            Directory.CreateDirectory(deploymentPath);

            // Download all files
            foreach (var file in deployment.Files)
            {
                var localPath = Path.Combine(deploymentPath, file);
                var localDir = Path.GetDirectoryName(localPath);

                if (!string.IsNullOrEmpty(localDir))
                {
                    Directory.CreateDirectory(localDir);
                }

                var fileData = await _apiClient.DownloadDeploymentFileAsync(deployment.DeploymentId, file);
                if (fileData != null)
                {
                    await File.WriteAllBytesAsync(localPath, fileData);
                    _logger.LogDebug("Downloaded file: {File}", file);
                }
            }

            // Execute based on deployment type
            DeploymentResultDto result;

            switch (deployment.Type)
            {
                case DeploymentType.Executable:
                case DeploymentType.MsiInstaller:
                    result = await ExecuteProcessAsync(deployment, deploymentPath, startTime);
                    break;

                case DeploymentType.PowerShell:
                    result = await ExecutePowerShellAsync(deployment, deploymentPath, startTime);
                    break;

                case DeploymentType.BatchScript:
                    result = await ExecuteBatchAsync(deployment, deploymentPath, startTime);
                    break;

                case DeploymentType.FilesCopy:
                    result = await CopyFilesAsync(deployment, deploymentPath, startTime);
                    break;

                default:
                    result = new DeploymentResultDto
                    {
                        DeploymentId = deployment.DeploymentId,
                        AgentId = _configuration.AgentId!.Value,
                        Status = DeploymentStatus.Failed,
                        StartedAt = startTime,
                        CompletedAt = DateTime.UtcNow,
                        ErrorMessage = $"Unsupported deployment type: {deployment.Type}"
                    };
                    break;
            }

            // Send result to server
            await _apiClient.UpdateDeploymentResultAsync(deployment.DeploymentId, result);

            _logger.LogInformation("Deployment {DeploymentId} completed with status {Status}",
                deployment.DeploymentId, result.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing deployment {DeploymentId}", deployment.DeploymentId);

            var errorResult = new DeploymentResultDto
            {
                DeploymentId = deployment.DeploymentId,
                AgentId = _configuration.AgentId!.Value,
                Status = DeploymentStatus.Failed,
                StartedAt = startTime,
                CompletedAt = DateTime.UtcNow,
                ErrorMessage = ex.Message
            };

            await _apiClient.UpdateDeploymentResultAsync(deployment.DeploymentId, errorResult);
        }
        finally
        {
            // Cleanup deployment directory
            try
            {
                if (Directory.Exists(deploymentPath))
                {
                    Directory.Delete(deploymentPath, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error cleaning up deployment directory");
            }
        }
    }

    private async Task<DeploymentResultDto> ExecuteProcessAsync(
        DeploymentTaskDto deployment, string deploymentPath, DateTime startTime)
    {
        var executablePath = string.IsNullOrEmpty(deployment.ExecutablePath)
            ? deployment.Files.FirstOrDefault()
            : deployment.ExecutablePath;

        if (string.IsNullOrEmpty(executablePath))
        {
            return new DeploymentResultDto
            {
                DeploymentId = deployment.DeploymentId,
                AgentId = _configuration.AgentId!.Value,
                Status = DeploymentStatus.Failed,
                StartedAt = startTime,
                CompletedAt = DateTime.UtcNow,
                ErrorMessage = "No executable specified"
            };
        }

        var fullPath = Path.Combine(deploymentPath, executablePath);

        var processInfo = new ProcessStartInfo
        {
            FileName = fullPath,
            Arguments = deployment.Arguments ?? string.Empty,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        if (deployment.RunAsAdmin && OperatingSystem.IsWindows())
        {
            processInfo.Verb = "runas";
        }

        return await RunProcessAsync(deployment, processInfo, startTime);
    }

    private async Task<DeploymentResultDto> ExecutePowerShellAsync(
        DeploymentTaskDto deployment, string deploymentPath, DateTime startTime)
    {
        var scriptPath = deployment.ExecutablePath ?? deployment.Files.FirstOrDefault(f => f.EndsWith(".ps1"));

        if (string.IsNullOrEmpty(scriptPath))
        {
            return new DeploymentResultDto
            {
                DeploymentId = deployment.DeploymentId,
                AgentId = _configuration.AgentId!.Value,
                Status = DeploymentStatus.Failed,
                StartedAt = startTime,
                CompletedAt = DateTime.UtcNow,
                ErrorMessage = "No PowerShell script specified"
            };
        }

        var fullPath = Path.Combine(deploymentPath, scriptPath);

        var processInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-ExecutionPolicy Bypass -File \"{fullPath}\" {deployment.Arguments}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        return await RunProcessAsync(deployment, processInfo, startTime);
    }

    private async Task<DeploymentResultDto> ExecuteBatchAsync(
        DeploymentTaskDto deployment, string deploymentPath, DateTime startTime)
    {
        var scriptPath = deployment.ExecutablePath ?? deployment.Files.FirstOrDefault(f => f.EndsWith(".bat") || f.EndsWith(".cmd"));

        if (string.IsNullOrEmpty(scriptPath))
        {
            return new DeploymentResultDto
            {
                DeploymentId = deployment.DeploymentId,
                AgentId = _configuration.AgentId!.Value,
                Status = DeploymentStatus.Failed,
                StartedAt = startTime,
                CompletedAt = DateTime.UtcNow,
                ErrorMessage = "No batch script specified"
            };
        }

        var fullPath = Path.Combine(deploymentPath, scriptPath);

        var processInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{fullPath}\" {deployment.Arguments}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = deploymentPath
        };

        return await RunProcessAsync(deployment, processInfo, startTime);
    }

    private async Task<DeploymentResultDto> CopyFilesAsync(
        DeploymentTaskDto deployment, string deploymentPath, DateTime startTime)
    {
        try
        {
            var targetPath = deployment.Arguments ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "SADAB", "Deployments", deployment.Name);

            Directory.CreateDirectory(targetPath);

            foreach (var file in deployment.Files)
            {
                var sourcePath = Path.Combine(deploymentPath, file);
                var destPath = Path.Combine(targetPath, file);
                var destDir = Path.GetDirectoryName(destPath);

                if (!string.IsNullOrEmpty(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                File.Copy(sourcePath, destPath, overwrite: true);
            }

            return new DeploymentResultDto
            {
                DeploymentId = deployment.DeploymentId,
                AgentId = _configuration.AgentId!.Value,
                Status = DeploymentStatus.Completed,
                StartedAt = startTime,
                CompletedAt = DateTime.UtcNow,
                ExitCode = 0,
                Output = $"Files copied to {targetPath}"
            };
        }
        catch (Exception ex)
        {
            return new DeploymentResultDto
            {
                DeploymentId = deployment.DeploymentId,
                AgentId = _configuration.AgentId!.Value,
                Status = DeploymentStatus.Failed,
                StartedAt = startTime,
                CompletedAt = DateTime.UtcNow,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<DeploymentResultDto> RunProcessAsync(
        DeploymentTaskDto deployment, ProcessStartInfo processInfo, DateTime startTime)
    {
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        try
        {
            using var process = new Process { StartInfo = processInfo };

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null) outputBuilder.AppendLine(e.Data);
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null) errorBuilder.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var timeout = TimeSpan.FromMinutes(deployment.TimeoutMinutes);
            var completed = await Task.Run(() => process.WaitForExit((int)timeout.TotalMilliseconds));

            if (!completed)
            {
                process.Kill(true);
                return new DeploymentResultDto
                {
                    DeploymentId = deployment.DeploymentId,
                    AgentId = _configuration.AgentId!.Value,
                    Status = DeploymentStatus.Failed,
                    StartedAt = startTime,
                    CompletedAt = DateTime.UtcNow,
                    ErrorMessage = "Deployment timed out"
                };
            }

            var exitCode = process.ExitCode;
            var status = exitCode == 0 ? DeploymentStatus.Completed : DeploymentStatus.Failed;

            return new DeploymentResultDto
            {
                DeploymentId = deployment.DeploymentId,
                AgentId = _configuration.AgentId!.Value,
                Status = status,
                StartedAt = startTime,
                CompletedAt = DateTime.UtcNow,
                ExitCode = exitCode,
                Output = outputBuilder.ToString(),
                ErrorMessage = errorBuilder.Length > 0 ? errorBuilder.ToString() : null
            };
        }
        catch (Exception ex)
        {
            return new DeploymentResultDto
            {
                DeploymentId = deployment.DeploymentId,
                AgentId = _configuration.AgentId!.Value,
                Status = DeploymentStatus.Failed,
                StartedAt = startTime,
                CompletedAt = DateTime.UtcNow,
                ErrorMessage = ex.Message,
                Output = outputBuilder.ToString()
            };
        }
    }
}
