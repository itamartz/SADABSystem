using SADAB.Agent.Configuration;
using SADAB.Shared.DTOs;
using SADAB.Shared.Enums;
using System.Diagnostics;
using System.Text;

namespace SADAB.Agent.Services;

public interface ICommandExecutorService
{
    Task ExecuteCommandAsync(CommandExecutionDto command);
}

public class CommandExecutorService : ICommandExecutorService
{
    private readonly IApiClientService _apiClient;
    private readonly AgentConfiguration _configuration;
    private readonly ILogger<CommandExecutorService> _logger;

    public CommandExecutorService(
        IApiClientService apiClient,
        AgentConfiguration configuration,
        ILogger<CommandExecutorService> logger)
    {
        _apiClient = apiClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task ExecuteCommandAsync(CommandExecutionDto command)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("Executing command {CommandId}: {Command}", command.Id, command.Command);

            var processInfo = new ProcessStartInfo
            {
                FileName = command.Command,
                Arguments = command.Arguments ?? string.Empty,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

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

            var timeout = TimeSpan.FromMinutes(5); // Default timeout
            var completed = await Task.Run(() => process.WaitForExit((int)timeout.TotalMilliseconds));

            CommandExecutionDto result;

            if (!completed)
            {
                process.Kill(true);
                result = new CommandExecutionDto
                {
                    Id = command.Id,
                    AgentId = command.AgentId,
                    Command = command.Command,
                    Arguments = command.Arguments,
                    Status = CommandExecutionStatus.Timeout,
                    RequestedAt = command.RequestedAt,
                    StartedAt = startTime,
                    CompletedAt = DateTime.UtcNow,
                    ErrorOutput = "Command execution timed out"
                };
            }
            else
            {
                var exitCode = process.ExitCode;
                var status = exitCode == 0 ? CommandExecutionStatus.Completed : CommandExecutionStatus.Failed;

                result = new CommandExecutionDto
                {
                    Id = command.Id,
                    AgentId = command.AgentId,
                    Command = command.Command,
                    Arguments = command.Arguments,
                    Status = status,
                    RequestedAt = command.RequestedAt,
                    StartedAt = startTime,
                    CompletedAt = DateTime.UtcNow,
                    ExitCode = exitCode,
                    Output = outputBuilder.ToString(),
                    ErrorOutput = errorBuilder.Length > 0 ? errorBuilder.ToString() : null
                };
            }

            await _apiClient.UpdateCommandResultAsync(command.Id, result);

            _logger.LogInformation("Command {CommandId} completed with status {Status}", command.Id, result.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command {CommandId}", command.Id);

            var errorResult = new CommandExecutionDto
            {
                Id = command.Id,
                AgentId = command.AgentId,
                Command = command.Command,
                Arguments = command.Arguments,
                Status = CommandExecutionStatus.Failed,
                RequestedAt = command.RequestedAt,
                StartedAt = startTime,
                CompletedAt = DateTime.UtcNow,
                ErrorOutput = ex.Message
            };

            await _apiClient.UpdateCommandResultAsync(command.Id, errorResult);
        }
    }
}
