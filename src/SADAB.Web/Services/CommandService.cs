using SADAB.Shared.DTOs;
using System.Net.Http.Json;

namespace SADAB.Web.Services;

public class CommandService : ICommandService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public CommandService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<List<CommandExecutionDto>> GetRecentCommandsAsync()
    {
        var client = _httpClientFactory.CreateClient("SADAB.API");
        var response = await client.GetAsync("/api/commands");

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<List<CommandExecutionDto>>() ?? new List<CommandExecutionDto>();
        }

        return new List<CommandExecutionDto>();
    }

    public async Task<CommandExecutionDto?> GetCommandByIdAsync(Guid id)
    {
        var client = _httpClientFactory.CreateClient("SADAB.API");
        var response = await client.GetAsync($"/api/commands/{id}");

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<CommandExecutionDto>();
        }

        return null;
    }

    public async Task<CommandExecutionDto> ExecuteCommandAsync(ExecuteCommandRequest request)
    {
        var client = _httpClientFactory.CreateClient("SADAB.API");
        var response = await client.PostAsJsonAsync("/api/commands", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CommandExecutionDto>()
            ?? throw new Exception("Failed to execute command");
    }
}
