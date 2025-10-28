using SADAB.Shared.DTOs;
using System.Net.Http.Json;

namespace SADAB.Web.Services;

public class AgentService : IAgentService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public AgentService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<List<AgentDto>> GetAllAgentsAsync()
    {
        var client = _httpClientFactory.CreateClient("SADAB.API");
        var response = await client.GetAsync("/api/agents");

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<List<AgentDto>>() ?? new List<AgentDto>();
        }

        return new List<AgentDto>();
    }

    public async Task<AgentDto?> GetAgentByIdAsync(Guid id)
    {
        var client = _httpClientFactory.CreateClient("SADAB.API");
        var response = await client.GetAsync($"/api/agents/{id}");

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<AgentDto>();
        }

        return null;
    }

    public async Task<bool> DeleteAgentAsync(Guid id)
    {
        var client = _httpClientFactory.CreateClient("SADAB.API");
        var response = await client.DeleteAsync($"/api/agents/{id}");
        return response.IsSuccessStatusCode;
    }
}
