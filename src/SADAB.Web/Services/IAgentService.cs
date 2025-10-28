using SADAB.Shared.DTOs;

namespace SADAB.Web.Services;

public interface IAgentService
{
    Task<List<AgentDto>> GetAllAgentsAsync();
    Task<AgentDto?> GetAgentByIdAsync(Guid id);
    Task<bool> DeleteAgentAsync(Guid id);
}
