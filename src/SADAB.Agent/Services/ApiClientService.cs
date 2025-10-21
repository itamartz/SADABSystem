using SADAB.Agent.Configuration;
using SADAB.Shared.DTOs;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace SADAB.Agent.Services;

public interface IApiClientService
{
    Task<AgentRegistrationResponse?> RegisterAsync(AgentRegistrationRequest request);
    Task<bool> SendHeartbeatAsync(AgentHeartbeatRequest request);
    Task<CertificateRefreshResponse?> RefreshCertificateAsync(CertificateRefreshRequest request);
    Task<List<DeploymentTaskDto>> GetPendingDeploymentsAsync();
    Task<bool> UpdateDeploymentResultAsync(Guid deploymentId, DeploymentResultDto result);
    Task<byte[]?> DownloadDeploymentFileAsync(Guid deploymentId, string filePath);
    Task<List<CommandExecutionDto>> GetPendingCommandsAsync();
    Task<bool> UpdateCommandResultAsync(Guid commandId, CommandExecutionDto result);
    Task<bool> SubmitInventoryAsync(InventoryDataDto inventory);
}

public class ApiClientService : IApiClientService
{
    private readonly HttpClient _httpClient;
    private readonly AgentConfiguration _configuration;
    private readonly ILogger<ApiClientService> _logger;

    public ApiClientService(
        HttpClient httpClient,
        AgentConfiguration configuration,
        ILogger<ApiClientService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_configuration.ServerUrl);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // Add certificate thumbprint to headers (for development)
        if (!string.IsNullOrEmpty(_configuration.CertificateThumbprint))
        {
            _httpClient.DefaultRequestHeaders.Add("X-Client-Certificate-Thumbprint", _configuration.CertificateThumbprint);
        }
    }

    public async Task<AgentRegistrationResponse?> RegisterAsync(AgentRegistrationRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/agents/register", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<AgentRegistrationResponse>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering agent");
            return null;
        }
    }

    public async Task<bool> SendHeartbeatAsync(AgentHeartbeatRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/agents/heartbeat", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending heartbeat");
            return false;
        }
    }

    public async Task<CertificateRefreshResponse?> RefreshCertificateAsync(CertificateRefreshRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/agents/refresh-certificate", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CertificateRefreshResponse>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing certificate");
            return null;
        }
    }

    public async Task<List<DeploymentTaskDto>> GetPendingDeploymentsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/deployments/pending");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<DeploymentTaskDto>>() ?? new List<DeploymentTaskDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending deployments");
            return new List<DeploymentTaskDto>();
        }
    }

    public async Task<bool> UpdateDeploymentResultAsync(Guid deploymentId, DeploymentResultDto result)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/deployments/{deploymentId}/results", result);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating deployment result");
            return false;
        }
    }

    public async Task<byte[]?> DownloadDeploymentFileAsync(Guid deploymentId, string filePath)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/deployments/files/{deploymentId}?filePath={Uri.EscapeDataString(filePath)}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading deployment file {FilePath}", filePath);
            return null;
        }
    }

    public async Task<List<CommandExecutionDto>> GetPendingCommandsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/commands/pending");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<CommandExecutionDto>>() ?? new List<CommandExecutionDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending commands");
            return new List<CommandExecutionDto>();
        }
    }

    public async Task<bool> UpdateCommandResultAsync(Guid commandId, CommandExecutionDto result)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/commands/{commandId}/result", result);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating command result");
            return false;
        }
    }

    public async Task<bool> SubmitInventoryAsync(InventoryDataDto inventory)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/inventory", inventory);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting inventory");
            return false;
        }
    }
}
