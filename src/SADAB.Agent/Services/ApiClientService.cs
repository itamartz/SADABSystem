using SADAB.Agent.Configuration;
using SADAB.Shared.DTOs;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

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
    private readonly IConfiguration _appConfiguration;
    private readonly ILogger<ApiClientService> _logger;
    private readonly string _certificateHeaderName;

    public ApiClientService(
        HttpClient httpClient,
        AgentConfiguration configuration,
        IConfiguration appConfiguration,
        ILogger<ApiClientService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _appConfiguration = appConfiguration;
        _logger = logger;

        _certificateHeaderName = _appConfiguration["ServiceSettings:CertificateHeaderName"] ?? "X-Client-Certificate-Thumbprint";

        _httpClient.BaseAddress = new Uri(_configuration.ServerUrl);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // Add certificate thumbprint to headers (for development)
        if (!string.IsNullOrEmpty(_configuration.CertificateThumbprint))
        {
            _httpClient.DefaultRequestHeaders.Add(_certificateHeaderName, _configuration.CertificateThumbprint);
        }
    }

    public async Task<AgentRegistrationResponse?> RegisterAsync(AgentRegistrationRequest request)
    {
        try
        {
            var endpoint = _appConfiguration["ApiEndpoints:Register"] ?? "/api/agents/register";
            var response = await _httpClient.PostAsJsonAsync(endpoint, request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<AgentRegistrationResponse>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, _appConfiguration["Messages:ErrorRegisteringAgent"] ?? "Error registering agent");
            return null;
        }
    }

    public async Task<bool> SendHeartbeatAsync(AgentHeartbeatRequest request)
    {
        try
        {
            var endpoint = _appConfiguration["ApiEndpoints:Heartbeat"] ?? "/api/agents/heartbeat";
            var response = await _httpClient.PostAsJsonAsync(endpoint, request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, _appConfiguration["Messages:ErrorSendingHeartbeat"] ?? "Error sending heartbeat");
            return false;
        }
    }

    public async Task<CertificateRefreshResponse?> RefreshCertificateAsync(CertificateRefreshRequest request)
    {
        try
        {
            var endpoint = _appConfiguration["ApiEndpoints:RefreshCertificate"] ?? "/api/agents/refresh-certificate";
            var response = await _httpClient.PostAsJsonAsync(endpoint, request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CertificateRefreshResponse>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, _appConfiguration["Messages:ErrorRefreshingCertificate"] ?? "Error refreshing certificate");
            return null;
        }
    }

    public async Task<List<DeploymentTaskDto>> GetPendingDeploymentsAsync()
    {
        try
        {
            var endpoint = _appConfiguration["ApiEndpoints:PendingDeployments"] ?? "/api/deployments/pending";
            var response = await _httpClient.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<DeploymentTaskDto>>() ?? new List<DeploymentTaskDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, _appConfiguration["Messages:ErrorGettingPendingDeployments"] ?? "Error getting pending deployments");
            return new List<DeploymentTaskDto>();
        }
    }

    public async Task<bool> UpdateDeploymentResultAsync(Guid deploymentId, DeploymentResultDto result)
    {
        try
        {
            var endpointTemplate = _appConfiguration["ApiEndpoints:DeploymentResults"] ?? "/api/deployments/{0}/results";
            var endpoint = string.Format(endpointTemplate, deploymentId);
            var response = await _httpClient.PostAsJsonAsync(endpoint, result);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, _appConfiguration["Messages:ErrorUpdatingDeploymentResult"] ?? "Error updating deployment result");
            return false;
        }
    }

    public async Task<byte[]?> DownloadDeploymentFileAsync(Guid deploymentId, string filePath)
    {
        try
        {
            var endpointTemplate = _appConfiguration["ApiEndpoints:DeploymentFiles"] ?? "/api/deployments/files/{0}";
            var endpoint = string.Format(endpointTemplate, deploymentId) + $"?filePath={Uri.EscapeDataString(filePath)}";
            var response = await _httpClient.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync();
        }
        catch (Exception ex)
        {
            var errorMessage = _appConfiguration["Messages:ErrorDownloadingDeploymentFile"] ?? "Error downloading deployment file {0}";
            _logger.LogError(ex, string.Format(errorMessage, filePath));
            return null;
        }
    }

    public async Task<List<CommandExecutionDto>> GetPendingCommandsAsync()
    {
        try
        {
            var endpoint = _appConfiguration["ApiEndpoints:PendingCommands"] ?? "/api/commands/pending";
            var response = await _httpClient.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<CommandExecutionDto>>() ?? new List<CommandExecutionDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, _appConfiguration["Messages:ErrorGettingPendingCommands"] ?? "Error getting pending commands");
            return new List<CommandExecutionDto>();
        }
    }

    public async Task<bool> UpdateCommandResultAsync(Guid commandId, CommandExecutionDto result)
    {
        try
        {
            var endpointTemplate = _appConfiguration["ApiEndpoints:CommandResult"] ?? "/api/commands/{0}/result";
            var endpoint = string.Format(endpointTemplate, commandId);
            var response = await _httpClient.PostAsJsonAsync(endpoint, result);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, _appConfiguration["Messages:ErrorUpdatingCommandResult"] ?? "Error updating command result");
            return false;
        }
    }

    public async Task<bool> SubmitInventoryAsync(InventoryDataDto inventory)
    {
        try
        {
            var endpoint = _appConfiguration["ApiEndpoints:Inventory"] ?? "/api/inventory";
            var response = await _httpClient.PostAsJsonAsync(endpoint, inventory);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, _appConfiguration["Messages:ErrorSubmittingInventory"] ?? "Error submitting inventory");
            return false;
        }
    }
}
