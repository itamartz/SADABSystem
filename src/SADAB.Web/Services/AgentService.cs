using SADAB.Shared.DTOs;
using SADAB.Web.Pages;
using System.Net.Http.Json;

namespace SADAB.Web.Services;

/// <summary>
/// Implementation of <see cref="IAgentService"/> for managing SADAB agents.
/// This service communicates with the backend REST API to perform agent-related operations.
/// It uses the named HTTP client "SADAB.API" configured with the base URL from application settings.
/// </summary>
/// <remarks>
/// This class is registered as a scoped service in the dependency injection container,
/// meaning each Blazor circuit (user session) gets its own instance. This ensures proper
/// isolation of HTTP requests and prevents cross-session data contamination.
/// </remarks>
public class AgentService : IAgentService
{
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentService"/> class.
    /// </summary>
    /// <param name="httpClientFactory">
    /// The HTTP client factory used to create named clients for API communication.
    /// This factory is provided by ASP.NET Core's dependency injection system.
    /// </param>
    public AgentService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Makes an HTTP GET request to /api/agents endpoint.
    /// If the request fails (non-success status code), returns an empty list rather than
    /// throwing an exception, allowing the UI to display a "no agents" state gracefully.
    /// </remarks>
    public async Task<List<AgentDto>> GetAllAgentsAsync()
    {
        // Create a named HTTP client configured with the SADAB API base URL
        var client = _httpClientFactory.CreateClient("SADAB.API");

        // Send GET request to retrieve all agents
        var response = await client.GetAsync("/api/agents");

        // Check if the request was successful (HTTP 2xx status code)
        if (response.IsSuccessStatusCode)
        {
            // Deserialize JSON response to AgentDto list
            var agents = await response.Content.ReadFromJsonAsync<List<AgentDto>>() ?? new List<AgentDto>();
            
            
            // Filter out localhost agent (127.0.0.1) from the list
            return agents
                .Where(a => a.IpAddress != "127.0.0.1")
                .Select(a =>
                {
                    if ((DateTime.Now - a.LastHeartbeat).TotalMinutes > 1)
                        a.Status = SADAB.Shared.Enums.AgentStatus.Offline;
                    return a;
                }).ToList();
        }

        // Return empty list on failure to allow graceful UI handling
        return new List<AgentDto>();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Makes an HTTP GET request to /api/agents/{id} endpoint.
    /// Returns null if the agent is not found (HTTP 404) or if any other error occurs,
    /// allowing calling code to handle the absence of data appropriately.
    /// </remarks>
    public async Task<AgentDto?> GetAgentByIdAsync(Guid id)
    {
        // Create a named HTTP client configured with the SADAB API base URL
        var client = _httpClientFactory.CreateClient("SADAB.API");

        // Send GET request for a specific agent by ID
        var response = await client.GetAsync($"/api/agents/{id}");

        // Check if the request was successful
        if (response.IsSuccessStatusCode)
        {
            // Deserialize JSON response to AgentDto object
            return await response.Content.ReadFromJsonAsync<AgentDto>();
        }

        // Return null if agent not found or request failed
        return null;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Makes an HTTP DELETE request to /api/agents/{id} endpoint.
    /// Returns true for successful deletion (HTTP 2xx), false otherwise.
    /// The backend API is responsible for handling cascade deletion of related data
    /// such as deployment history, command logs, and certificates.
    /// </remarks>
    public async Task<bool> DeleteAgentAsync(Guid id)
    {
        // Create a named HTTP client configured with the SADAB API base URL
        var client = _httpClientFactory.CreateClient("SADAB.API");

        // Send DELETE request to remove the agent
        var response = await client.DeleteAsync($"/api/agents/{id}");

        // Return true if deletion was successful (HTTP 2xx status code)
        return response.IsSuccessStatusCode;
    }
}
