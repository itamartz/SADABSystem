using SADAB.Shared.DTOs;
using System.Net.Http.Json;

namespace SADAB.Web.Services;

/// <summary>
/// Implementation of <see cref="ICommandService"/> for executing and managing remote commands.
/// This service communicates with the backend REST API to queue command executions on agents
/// and retrieve their results. It supports PowerShell, shell commands, and administrative operations.
/// </summary>
/// <remarks>
/// This class is registered as a scoped service in the dependency injection container.
/// Each Blazor user session gets its own instance for proper request isolation.
/// Commands are executed asynchronously on agents - this service only queues the command
/// and retrieves status/output; actual execution happens on the agent machines.
/// </remarks>
public class CommandService : ICommandService
{
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandService"/> class.
    /// </summary>
    /// <param name="httpClientFactory">
    /// The HTTP client factory used to create named clients for API communication.
    /// This factory provides efficient connection pooling and lifecycle management.
    /// </param>
    public CommandService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Makes an HTTP GET request to /api/commands endpoint.
    /// Returns an empty list on failure rather than throwing an exception,
    /// allowing the UI to display an empty state gracefully. The API typically
    /// returns commands ordered by request timestamp (most recent first).
    /// </remarks>
    public async Task<List<CommandExecutionDto>> GetRecentCommandsAsync()
    {
        // Create a named HTTP client with pre-configured base URL
        var client = _httpClientFactory.CreateClient("SADAB.API");

        // Send GET request to retrieve recent command executions
        var response = await client.GetAsync("/api/commands");

        // Check if the request succeeded
        if (response.IsSuccessStatusCode)
        {
            // Deserialize JSON response to CommandExecutionDto list
            // Return empty list if deserialization returns null
            return await response.Content.ReadFromJsonAsync<List<CommandExecutionDto>>() ?? new List<CommandExecutionDto>();
        }

        // Return empty list on failure for graceful UI handling
        return new List<CommandExecutionDto>();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Makes an HTTP GET request to /api/commands/{id} endpoint.
    /// Returns null if the command execution is not found or if the request fails.
    /// This method retrieves complete execution details including full stdout/stderr output,
    /// which may be truncated in list views for performance reasons.
    /// </remarks>
    public async Task<CommandExecutionDto?> GetCommandByIdAsync(Guid id)
    {
        // Create a named HTTP client with pre-configured base URL
        var client = _httpClientFactory.CreateClient("SADAB.API");

        // Send GET request for a specific command execution by ID
        var response = await client.GetAsync($"/api/commands/{id}");

        // Check if the request succeeded
        if (response.IsSuccessStatusCode)
        {
            // Deserialize JSON response to CommandExecutionDto object
            return await response.Content.ReadFromJsonAsync<CommandExecutionDto>();
        }

        // Return null if command execution not found or request failed
        return null;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Makes an HTTP POST request to /api/commands endpoint with the command execution
    /// request in the request body. This method throws an exception if the request fails,
    /// allowing the calling code to handle and display validation errors or connectivity
    /// issues to the user. The command is queued for execution; the returned DTO contains
    /// the execution ID but may not yet have output or completion status.
    /// </remarks>
    /// <exception cref="HttpRequestException">
    /// Thrown if the HTTP request fails or returns a non-success status code.
    /// </exception>
    public async Task<CommandExecutionDto> ExecuteCommandAsync(ExecuteCommandRequest request)
    {
        // Create a named HTTP client with pre-configured base URL
        var client = _httpClientFactory.CreateClient("SADAB.API");

        // Send POST request with command execution configuration as JSON body
        var response = await client.PostAsJsonAsync("/api/commands", request);

        // Throw exception if request failed (allows UI to display error messages)
        response.EnsureSuccessStatusCode();

        // Deserialize and return the command execution record
        // Throw exception if response body is null (unexpected API behavior)
        return await response.Content.ReadFromJsonAsync<CommandExecutionDto>()
            ?? throw new Exception("Failed to execute command");
    }
}
