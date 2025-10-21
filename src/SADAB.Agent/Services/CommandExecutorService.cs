using SADAB.Agent.Configuration;
using SADAB.Shared.DTOs;
using SADAB.Shared.Enums;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace SADAB.Agent.Services;

public interface ICommandExecutorService
{
    Task ExecuteCommandAsync(CommandExecutionDto command);
}

public class CommandExecutorService : ICommandExecutorService
{
    private readonly IApiClientService _apiClient;
    private readonly AgentConfiguration _configuration;
    private readonly IConfiguration _appConfiguration;
    private readonly ILogger<CommandExecutorService> _logger;
    private readonly int _defaultTimeoutMinutes;

    public CommandExecutorService(
        IApiClientService apiClient,
        AgentConfiguration configuration,
        IConfiguration appConfiguration,
        ILogger<CommandExecutorService> logger)
    {
        _apiClient = apiClient;
        _configuration = configuration;
        _appConfiguration = appConfiguration;
        _logger = logger;

        _defaultTimeoutMinutes = _appConfiguration.GetValue<int>("CommandSettings:DefaultTimeoutMinutes");
    }

    public async Task ExecuteCommandAsync(CommandExecutionDto command)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            var executingMessage = _appConfiguration["Messages:ExecutingCommand"] ?? "Executing command {0}: {1}";
            _logger.LogInformation(string.Format(executingMessage, command.Id, command.Command));

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

            var timeout = TimeSpan.FromMinutes(_defaultTimeoutMinutes);
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
                    ErrorOutput = _appConfiguration["Messages:CommandExecutionTimedOut"] ?? "Command execution timed out"
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

            var completedMessage = _appConfiguration["Messages:CommandCompleted"] ?? "Command {0} completed with status {1}";
            _logger.LogInformation(string.Format(completedMessage, command.Id, result.Status));
        }
        catch (Exception ex)
        {
            var errorMessage = _appConfiguration["Messages:ErrorExecutingCommand"] ?? "Error executing command {0}";
            _logger.LogError(ex, string.Format(errorMessage, command.Id));

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
