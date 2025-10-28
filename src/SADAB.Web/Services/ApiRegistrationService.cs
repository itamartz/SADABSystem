using System.Net.Http.Json;
using SADAB.Shared.DTOs;
using SADAB.Shared.Enums;

namespace SADAB.Web.Services;

/// <summary>
/// Implementation of API registration service that registers the web app as an agent
/// </summary>
public class ApiRegistrationService : IApiRegistrationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ApiRegistrationService> _logger;

    public ApiRegistrationService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ApiRegistrationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AgentRegistrationResponse?> RegisterWebAppAsync()
    {
        try
        {
            // Create a temporary HTTP client without certificate authentication
            // (registration endpoint allows anonymous access)
            var client = _httpClientFactory.CreateClient("SADAB.API.Anonymous");

            // Generate unique machine ID for web app
            var machineId = GetOrCreateMachineId();

            var registrationRequest = new AgentRegistrationRequest
            {
                MachineName = Environment.MachineName + "-WebApp",
                MachineId = machineId,
                OperatingSystem = $"{Environment.OSVersion.Platform} {Environment.OSVersion.Version}",
                IpAddress = "127.0.0.1", // Web app is on same server as API
                Metadata = new Dictionary<string, string>
                {
                    { "Type", "WebApplication" },
                    { "Version", "1.0.0" },
                    { "Framework", "ASP.NET Core" }
                }
            };

            _logger.LogInformation("Registering web app with API. MachineId: {MachineId}", machineId);

            var response = await client.PostAsJsonAsync("/api/agents/register", registrationRequest);

            if (response.IsSuccessStatusCode)
            {
                var registrationResponse = await response.Content.ReadFromJsonAsync<AgentRegistrationResponse>();
                _logger.LogInformation("Web app registered successfully. AgentId: {AgentId}", registrationResponse?.AgentId);
                return registrationResponse;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to register web app. Status: {Status}, Error: {Error}",
                    response.StatusCode, errorContent);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering web app with API");
            return null;
        }
    }

    /// <summary>
    /// Gets or creates a persistent machine ID for the web app
    /// </summary>
    private string GetOrCreateMachineId()
    {
        var machineIdPath = Path.Combine(AppContext.BaseDirectory, "Data", "machine-id.txt");

        if (File.Exists(machineIdPath))
        {
            return File.ReadAllText(machineIdPath).Trim();
        }

        // Generate new machine ID
        var machineId = $"WEBAPP-{Guid.NewGuid():N}";

        // Ensure directory exists
        var directory = Path.GetDirectoryName(machineIdPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(machineIdPath, machineId);
        _logger.LogInformation("Generated new machine ID for web app: {MachineId}", machineId);

        return machineId;
    }
}
