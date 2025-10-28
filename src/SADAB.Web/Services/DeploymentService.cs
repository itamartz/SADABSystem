using SADAB.Shared.DTOs;
using System.Net.Http.Json;

namespace SADAB.Web.Services;

public class DeploymentService : IDeploymentService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public DeploymentService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<List<DeploymentDto>> GetAllDeploymentsAsync()
    {
        var client = _httpClientFactory.CreateClient("SADAB.API");
        var response = await client.GetAsync("/api/deployments");

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<List<DeploymentDto>>() ?? new List<DeploymentDto>();
        }

        return new List<DeploymentDto>();
    }

    public async Task<DeploymentDto?> GetDeploymentByIdAsync(Guid id)
    {
        var client = _httpClientFactory.CreateClient("SADAB.API");
        var response = await client.GetAsync($"/api/deployments/{id}");

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<DeploymentDto>();
        }

        return null;
    }

    public async Task<DeploymentDto> CreateDeploymentAsync(CreateDeploymentRequest request)
    {
        var client = _httpClientFactory.CreateClient("SADAB.API");
        var response = await client.PostAsJsonAsync("/api/deployments", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DeploymentDto>()
            ?? throw new Exception("Failed to create deployment");
    }

    public async Task<bool> StartDeploymentAsync(Guid id)
    {
        var client = _httpClientFactory.CreateClient("SADAB.API");
        var response = await client.PostAsync($"/api/deployments/{id}/start", null);
        return response.IsSuccessStatusCode;
    }
}
