using SADAB.Shared.DTOs;

namespace SADAB.Web.Services;

public interface ICommandService
{
    Task<List<CommandExecutionDto>> GetRecentCommandsAsync();
    Task<CommandExecutionDto?> GetCommandByIdAsync(Guid id);
    Task<CommandExecutionDto> ExecuteCommandAsync(ExecuteCommandRequest request);
}
