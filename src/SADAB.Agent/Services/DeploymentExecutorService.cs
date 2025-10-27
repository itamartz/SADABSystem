using SADAB.Agent.Configuration;
using SADAB.Shared.DTOs;
using SADAB.Shared.Enums;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace SADAB.Agent.Services;

public interface IDeploymentExecutorService
{
    Task ExecuteDeploymentAsync(DeploymentTaskDto deployment);
}

public class DeploymentExecutorService : IDeploymentExecutorService
{
    private readonly IApiClientService _apiClient;
    private readonly AgentConfiguration _configuration;
    private readonly IConfiguration _appConfiguration;
    private readonly ILogger<DeploymentExecutorService> _logger;
    private readonly string _deploymentsSubFolder;
    private readonly string _powerShellExe;
    private readonly string _powerShellArgs;
    private readonly string _cmdExe;
    private readonly string _cmdArgs;
    private readonly string _psExtension;
    private readonly string _batExtension;
    private readonly string _cmdExtension;

    public DeploymentExecutorService(
        IApiClientService apiClient,
        AgentConfiguration configuration,
        IConfiguration appConfiguration,
        ILogger<DeploymentExecutorService> logger)
    {
        _apiClient = apiClient;
        _configuration = configuration;
        _appConfiguration = appConfiguration;
        _logger = logger;

        _deploymentsSubFolder = _appConfiguration["PathSettings:DeploymentsSubFolder"] ?? "Deployments";
        _powerShellExe = _appConfiguration["ExecutableSettings:PowerShell"] ?? "powershell.exe";
        _powerShellArgs = _appConfiguration["ExecutableSettings:PowerShellArgs"] ?? "-ExecutionPolicy Bypass -File";
        _cmdExe = _appConfiguration["ExecutableSettings:CommandPrompt"] ?? "cmd.exe";
        _cmdArgs = _appConfiguration["ExecutableSettings:CommandPromptArgs"] ?? "/c";
        _psExtension = _appConfiguration["FileExtensions:PowerShell"] ?? ".ps1";
        _batExtension = _appConfiguration["FileExtensions:BatchScript"] ?? ".bat";
        _cmdExtension = _appConfiguration["FileExtensions:CommandScript"] ?? ".cmd";
    }

    public async Task ExecuteDeploymentAsync(DeploymentTaskDto deployment)
    {
        var deploymentPath = Path.Combine(_configuration.WorkingDirectory, _deploymentsSubFolder, deployment.DeploymentId.ToString());
        var startTime = DateTime.UtcNow;

        try
        {
            var startingMessage = _appConfiguration["Messages:StartingDeployment"] ?? "Starting deployment {0}: {1}";
            _logger.LogInformation(string.Format(startingMessage, deployment.DeploymentId, deployment.Name));

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
                    var downloadedMessage = _appConfiguration["Messages:DownloadedFile"] ?? "Downloaded file: {0}";
                    _logger.LogDebug(string.Format(downloadedMessage, file));
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
                    var unsupportedMessage = _appConfiguration["Messages:UnsupportedDeploymentType"] ?? "Unsupported deployment type: {0}";
                    result = new DeploymentResultDto
                    {
                        DeploymentId = deployment.DeploymentId,
                        AgentId = _configuration.AgentId!.Value,
                        Status = DeploymentStatus.Failed,
                        StartedAt = startTime,
                        CompletedAt = DateTime.UtcNow,
                        ErrorMessage = string.Format(unsupportedMessage, deployment.Type)
                    };
                    break;
            }

            // Send result to server
            await _apiClient.UpdateDeploymentResultAsync(deployment.DeploymentId, result);

            var completedMessage = _appConfiguration["Messages:DeploymentCompleted"] ?? "Deployment {0} completed with status {1}";
            _logger.LogInformation(string.Format(completedMessage, deployment.DeploymentId, result.Status));
        }
        catch (Exception ex)
        {
            var errorMessage = _appConfiguration["Messages:ErrorExecutingDeployment"] ?? "Error executing deployment {0}";
            _logger.LogError(ex, string.Format(errorMessage, deployment.DeploymentId));

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
                _logger.LogWarning(ex, _appConfiguration["Messages:ErrorCleanupDeploymentDirectory"] ?? "Error cleaning up deployment directory");
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
                ErrorMessage = _appConfiguration["Messages:NoExecutableSpecified"] ?? "No executable specified"
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
        var scriptPath = deployment.ExecutablePath ?? deployment.Files.FirstOrDefault(f => f.EndsWith(_psExtension));

        if (string.IsNullOrEmpty(scriptPath))
        {
            return new DeploymentResultDto
            {
                DeploymentId = deployment.DeploymentId,
                AgentId = _configuration.AgentId!.Value,
                Status = DeploymentStatus.Failed,
                StartedAt = startTime,
                CompletedAt = DateTime.UtcNow,
                ErrorMessage = _appConfiguration["Messages:NoPowerShellScriptSpecified"] ?? "No PowerShell script specified"
            };
        }

        var fullPath = Path.Combine(deploymentPath, scriptPath);

        var processInfo = new ProcessStartInfo
        {
            FileName = _powerShellExe,
            Arguments = $"{_powerShellArgs} \"{fullPath}\" {deployment.Arguments}",
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
        var scriptPath = deployment.ExecutablePath ?? deployment.Files.FirstOrDefault(f => f.EndsWith(_batExtension) || f.EndsWith(_cmdExtension));

        if (string.IsNullOrEmpty(scriptPath))
        {
            return new DeploymentResultDto
            {
                DeploymentId = deployment.DeploymentId,
                AgentId = _configuration.AgentId!.Value,
                Status = DeploymentStatus.Failed,
                StartedAt = startTime,
                CompletedAt = DateTime.UtcNow,
                ErrorMessage = _appConfiguration["Messages:NoBatchScriptSpecified"] ?? "No batch script specified"
            };
        }

        var fullPath = Path.Combine(deploymentPath, scriptPath);

        var processInfo = new ProcessStartInfo
        {
            FileName = _cmdExe,
            Arguments = $"{_cmdArgs} \"{fullPath}\" {deployment.Arguments}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = deploymentPath
        };

        return await RunProcessAsync(deployment, processInfo, startTime);
    }

    private async Task<DeploymentResultDto> CopyFilesAsync(DeploymentTaskDto deployment, string deploymentPath, DateTime startTime)
    {
        try
        {
            var programDataFolder = _appConfiguration["PathSettings:ProgramDataFolder"] ?? "SADAB";
            var deploymentsSubFolder = _appConfiguration["PathSettings:DeploymentsSubFolder"] ?? "Deployments";

            var targetPath = deployment.Arguments ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), programDataFolder, deploymentsSubFolder, deployment.Name);

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

            var copiedMessage = _appConfiguration["Messages:FilesCopiedTo"] ?? "Files copied to {0}";
            await Task.Delay(TimeSpan.FromSeconds(1));

            return new DeploymentResultDto
            {
                DeploymentId = deployment.DeploymentId,
                AgentId = _configuration.AgentId!.Value,
                Status = DeploymentStatus.Completed,
                StartedAt = startTime,
                CompletedAt = DateTime.UtcNow,
                ExitCode = 0,
                Output = string.Format(copiedMessage, targetPath)
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
                    ErrorMessage = _appConfiguration["Messages:DeploymentTimedOut"] ?? "Deployment timed out"
                };
            }

            var exitCode = process.ExitCode;

            // Check if exit code is in the list of success codes
            var isSuccess = deployment.SuccessExitCodes?.Contains(exitCode) ?? (exitCode == 0);
            var status = isSuccess ? DeploymentStatus.Completed : DeploymentStatus.Failed;

            _logger.LogDebug("Process exited with code {ExitCode}. Success codes: [{SuccessCodes}]. Status: {Status}",
                exitCode,
                deployment.SuccessExitCodes != null ? string.Join(", ", deployment.SuccessExitCodes) : "0",
                status);

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
