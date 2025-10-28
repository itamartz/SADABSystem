using SADAB.Shared.DTOs;
using System.Net.Http.Json;

namespace SADAB.Web.Services;

/// <summary>
/// Implementation of <see cref="IDeploymentService"/> for managing software deployments.
/// This service communicates with the backend REST API to orchestrate deployment operations
/// across multiple agents. It handles deployment creation, status monitoring, and execution control.
/// </summary>
/// <remarks>
/// This class is registered as a scoped service in the dependency injection container.
/// Each Blazor user session maintains its own instance to ensure proper request isolation.
/// The service uses JSON serialization for request/response bodies and handles HTTP status codes
/// appropriately to provide meaningful feedback to the UI layer.
/// </remarks>
public class DeploymentService : IDeploymentService
{
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeploymentService"/> class.
    /// </summary>
    /// <param name="httpClientFactory">
    /// The HTTP client factory used to create named clients for API communication.
    /// This factory manages connection pooling and client lifecycle automatically.
    /// </param>
    public DeploymentService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Makes an HTTP GET request to /api/deployments endpoint.
    /// Returns an empty list on failure rather than throwing an exception,
    /// allowing the UI to display an empty state gracefully. This method retrieves
    /// summary information for all deployments including status and statistics.
    /// </remarks>
    public async Task<List<DeploymentDto>> GetAllDeploymentsAsync()
    {
        // Create a named HTTP client with pre-configured base URL
        var client = _httpClientFactory.CreateClient("SADAB.API");

        // Send GET request to retrieve all deployments
        var response = await client.GetAsync("/api/deployments");

        // Check if the request succeeded
        if (response.IsSuccessStatusCode)
        {
            // Deserialize JSON response to DeploymentDto list
            // Return empty list if deserialization returns null
            return await response.Content.ReadFromJsonAsync<List<DeploymentDto>>() ?? new List<DeploymentDto>();
        }

        // Return empty list on failure for graceful UI handling
        return new List<DeploymentDto>();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Makes an HTTP GET request to /api/deployments/{id} endpoint.
    /// Returns null if the deployment is not found or if the request fails.
    /// This method retrieves detailed deployment information including per-agent
    /// execution results, output logs, and timing data.
    /// </remarks>
    public async Task<DeploymentDto?> GetDeploymentByIdAsync(Guid id)
    {
        // Create a named HTTP client with pre-configured base URL
        var client = _httpClientFactory.CreateClient("SADAB.API");

        // Send GET request for a specific deployment by ID
        var response = await client.GetAsync($"/api/deployments/{id}");

        // Check if the request succeeded
        if (response.IsSuccessStatusCode)
        {
            // Deserialize JSON response to DeploymentDto object
            return await response.Content.ReadFromJsonAsync<DeploymentDto>();
        }

        // Return null if deployment not found or request failed
        return null;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Makes an HTTP POST request to /api/deployments endpoint with the deployment
    /// configuration in the request body. This method throws an exception if the request
    /// fails (non-success status code), allowing the calling code to handle and display
    /// validation errors or API failures to the user.
    /// </remarks>
    /// <exception cref="HttpRequestException">
    /// Thrown if the HTTP request fails or returns a non-success status code.
    /// </exception>
    public async Task<DeploymentDto> CreateDeploymentAsync(CreateDeploymentRequest request)
    {
        // Create a named HTTP client with pre-configured base URL
        var client = _httpClientFactory.CreateClient("SADAB.API");

        // Send POST request with deployment configuration as JSON body
        var response = await client.PostAsJsonAsync("/api/deployments", request);

        // Throw exception if request failed (allows UI to display error messages)
        response.EnsureSuccessStatusCode();

        // Deserialize and return the created deployment
        // Throw exception if response body is null (unexpected API behavior)
        return await response.Content.ReadFromJsonAsync<DeploymentDto>()
            ?? throw new Exception("Failed to create deployment");
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Makes an HTTP POST request to /api/deployments/{id}/start endpoint.
    /// Returns true if the deployment was successfully queued for execution,
    /// false otherwise. This operation is idempotent - starting an already-running
    /// deployment will not cause errors but may return false.
    /// </remarks>
    public async Task<bool> StartDeploymentAsync(Guid id)
    {
        // Create a named HTTP client with pre-configured base URL
        var client = _httpClientFactory.CreateClient("SADAB.API");

        // Send POST request to start deployment execution
        // Null content because this endpoint doesn't require a request body
        var response = await client.PostAsync($"/api/deployments/{id}/start", null);

        // Return true if deployment was started successfully (HTTP 2xx status code)
        return response.IsSuccessStatusCode;
    }
}
