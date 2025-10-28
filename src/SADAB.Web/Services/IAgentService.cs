using SADAB.Shared.DTOs;

namespace SADAB.Web.Services;

/// <summary>
/// Service interface for managing SADAB agents in the web application.
/// Provides methods to retrieve and manage agent information from the backend API.
/// This service acts as an abstraction layer between Blazor components and the HTTP API,
/// enabling dependency injection and testability.
/// </summary>
public interface IAgentService
{
    /// <summary>
    /// Retrieves all registered agents from the SADAB server.
    /// </summary>
    /// <returns>
    /// A list of <see cref="AgentDto"/> objects representing all agents in the system.
    /// Returns an empty list if no agents are found or if the API request fails.
    /// </returns>
    /// <remarks>
    /// This method is used by the Dashboard and Agents pages to display a list of all
    /// registered machines. The returned data includes agent status, operating system,
    /// IP address, and last heartbeat information.
    /// </remarks>
    Task<List<AgentDto>> GetAllAgentsAsync();

    /// <summary>
    /// Retrieves detailed information for a specific agent by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier (GUID) of the agent to retrieve.</param>
    /// <returns>
    /// An <see cref="AgentDto"/> object containing the agent's information,
    /// or null if the agent is not found or the API request fails.
    /// </returns>
    /// <remarks>
    /// This method is typically used when navigating to an agent detail view or when
    /// refreshing information for a specific agent. It provides comprehensive details
    /// including certificate expiration dates and system metadata.
    /// </remarks>
    Task<AgentDto?> GetAgentByIdAsync(Guid id);

    /// <summary>
    /// Deletes an agent from the SADAB system.
    /// </summary>
    /// <param name="id">The unique identifier (GUID) of the agent to delete.</param>
    /// <returns>
    /// True if the agent was successfully deleted; false if the deletion failed
    /// or if the agent was not found.
    /// </returns>
    /// <remarks>
    /// This is a destructive operation that removes the agent and all associated data,
    /// including deployment history, command execution records, and inventory data.
    /// Use with caution and consider implementing confirmation dialogs in the UI.
    /// </remarks>
    Task<bool> DeleteAgentAsync(Guid id);
}
