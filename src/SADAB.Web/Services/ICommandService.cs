using SADAB.Shared.DTOs;

namespace SADAB.Web.Services;

/// <summary>
/// Service interface for executing and managing remote commands on SADAB agents.
/// Provides methods to execute PowerShell scripts, shell commands, and batch operations
/// on managed machines. This service enables real-time remote administration capabilities
/// through the SADAB web interface.
/// </summary>
public interface ICommandService
{
    /// <summary>
    /// Retrieves a list of recently executed commands across all agents.
    /// </summary>
    /// <returns>
    /// A list of <see cref="CommandExecutionDto"/> objects representing recent command
    /// executions with their status, output, and timing information.
    /// Returns an empty list if no commands have been executed or if the API request fails.
    /// </returns>
    /// <remarks>
    /// This method is used by the Dashboard and Commands pages to display command history.
    /// The results include command text, target agent information, execution status,
    /// exit codes, standard output, and error output. Commands are typically ordered by
    /// request timestamp with the most recent appearing first.
    /// </remarks>
    Task<List<CommandExecutionDto>> GetRecentCommandsAsync();

    /// <summary>
    /// Retrieves detailed information for a specific command execution by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier (GUID) of the command execution to retrieve.</param>
    /// <returns>
    /// A <see cref="CommandExecutionDto"/> object containing comprehensive command execution details,
    /// or null if the command is not found or the API request fails.
    /// </returns>
    /// <remarks>
    /// This method provides full command execution details including the complete standard output,
    /// error output, exit code, execution duration, and agent information. It is useful for
    /// debugging failed commands or reviewing detailed execution logs.
    /// </remarks>
    Task<CommandExecutionDto?> GetCommandByIdAsync(Guid id);

    /// <summary>
    /// Executes a command on one or more target agents.
    /// </summary>
    /// <param name="request">
    /// An <see cref="ExecuteCommandRequest"/> object containing the command to execute,
    /// optional arguments, target agent IDs, privilege elevation settings, and timeout configuration.
    /// </param>
    /// <returns>
    /// A <see cref="CommandExecutionDto"/> object representing the command execution
    /// with its unique identifier and initial status.
    /// </returns>
    /// <exception cref="Exception">
    /// Thrown if the command execution fails due to validation errors, agent connectivity issues,
    /// or API communication problems.
    /// </exception>
    /// <remarks>
    /// This method queues a command for asynchronous execution on the target agent(s).
    /// The command executes in the context of the agent's service account unless RunAsAdmin
    /// is specified. For PowerShell commands, ensure the command text is properly escaped.
    /// The execution is asynchronous - poll <see cref="GetCommandByIdAsync"/> to monitor progress
    /// and retrieve output once completed.
    /// </remarks>
    Task<CommandExecutionDto> ExecuteCommandAsync(ExecuteCommandRequest request);
}
