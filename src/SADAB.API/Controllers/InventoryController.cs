using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SADAB.API.Data;
using SADAB.API.Models;
using SADAB.Shared.DTOs;
using System.Text.Json;

namespace SADAB.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InventoryController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InventoryController> _logger;

    public InventoryController(ApplicationDbContext context, IConfiguration configuration, ILogger<InventoryController> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> SubmitInventory([FromBody] InventoryDataDto inventoryDto)
    {
        try
        {
            /*
            var agentIdClaim = User.FindFirst("AgentId")?.Value;
            _logger.LogDebug("Received inventory submission from AgentId claim: {AgentIdClaim}", agentIdClaim);

            if (string.IsNullOrEmpty(agentIdClaim) || !Guid.TryParse(agentIdClaim, out var agentId))
            {
                _logger.LogWarning("Invalid or missing AgentId claim");
                return Unauthorized();
            }

            if (inventoryDto.AgentId != agentId)
            {
                _logger.LogWarning("AgentId in payload does not match AgentId claim");
                return Forbid();
            }
             */

            var inventory = await _context.InventoryData.FirstOrDefaultAsync(e => e.AgentId == inventoryDto.AgentId);
            if (inventory != null)
            {
                _logger.LogDebug("Existing inventory record found for AgentId {AgentId}, updating it", inventoryDto.AgentId);
                inventory.HardwareInfo = JsonSerializer.Serialize(inventoryDto.HardwareInfo);
                inventory.InstalledSoftware = JsonSerializer.Serialize(inventoryDto.InstalledSoftware);
                inventory.EnvironmentVariables = JsonSerializer.Serialize(inventoryDto.EnvironmentVariables);
                inventory.RunningServices = JsonSerializer.Serialize(inventoryDto.RunningServices);
                inventory.CollectedAt = DateTime.UtcNow;

                _logger.LogInformation("Updating existing inventory record for AgentId {AgentId}", inventoryDto.AgentId);
                _context.InventoryData.Update(inventory);
                await _context.SaveChangesAsync();

                _logger.LogDebug("Inventory Data: {InventoryData}", inventory);
                return Ok(new { message = _configuration["Messages:InventoryDataUpdated"] ?? "Inventory data updated" });
            }
            else
            {
                inventory = new InventoryData
                {
                    Id = Guid.NewGuid(),
                    AgentId = inventoryDto.AgentId,
                    HardwareInfo = JsonSerializer.Serialize(inventoryDto.HardwareInfo),
                    InstalledSoftware = JsonSerializer.Serialize(inventoryDto.InstalledSoftware),
                    EnvironmentVariables = JsonSerializer.Serialize(inventoryDto.EnvironmentVariables),
                    RunningServices = JsonSerializer.Serialize(inventoryDto.RunningServices),
                    CollectedAt = DateTime.UtcNow
                };

                _context.InventoryData.Add(inventory);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Inventory data received from agent {AgentId}", inventoryDto.AgentId);
            }

            _logger.LogDebug("Inventory Data: {InventoryData}", inventory);
            return Ok(new { message = _configuration["Messages:InventoryDataReceived"] ?? "Inventory data received" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting inventory data");
            return StatusCode(500, new { message = _configuration["Messages:ErrorOccurred"] ?? "An error occurred" });
        }
    }

    [HttpGet("agent/{agentId}")]
    public async Task<ActionResult<InventoryDataDto>> GetAgentInventory(Guid agentId)
    {
        try
        {
            var inventory = await _context.InventoryData
                .Where(i => i.AgentId == agentId)
                .OrderByDescending(i => i.CollectedAt)
                .FirstOrDefaultAsync();

            if (inventory == null)
            {
                return NotFound();
            }

            var dto = new InventoryDataDto
            {
                AgentId = inventory.AgentId,
                HardwareInfo = JsonSerializer.Deserialize<Dictionary<string, object>>(inventory.HardwareInfo) ?? new(),
                InstalledSoftware = JsonSerializer.Deserialize<List<InstalledSoftwareDto>>(inventory.InstalledSoftware) ?? new(),
                EnvironmentVariables = JsonSerializer.Deserialize<Dictionary<string, string>>(inventory.EnvironmentVariables) ?? new(),
                RunningServices = JsonSerializer.Deserialize<List<string>>(inventory.RunningServices) ?? new(),
                CollectedAt = inventory.CollectedAt
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving inventory for agent {AgentId}", agentId);
            return StatusCode(500, new { message = _configuration["Messages:ErrorOccurred"] ?? "An error occurred" });
        }
    }

    [HttpGet("agent/{agentId}/history")]
    public async Task<ActionResult<List<InventoryDataDto>>> GetAgentInventoryHistory(Guid agentId, [FromQuery] int limit = 10)
    {
        try
        {
            var inventories = await _context.InventoryData
                .Where(i => i.AgentId == agentId)
                .OrderByDescending(i => i.CollectedAt)
                .Take(limit)
                .ToListAsync();

            var dtos = inventories.Select(inventory => new InventoryDataDto
            {
                AgentId = inventory.AgentId,
                HardwareInfo = JsonSerializer.Deserialize<Dictionary<string, object>>(inventory.HardwareInfo) ?? new(),
                InstalledSoftware = JsonSerializer.Deserialize<List<InstalledSoftwareDto>>(inventory.InstalledSoftware) ?? new(),
                EnvironmentVariables = JsonSerializer.Deserialize<Dictionary<string, string>>(inventory.EnvironmentVariables) ?? new(),
                RunningServices = JsonSerializer.Deserialize<List<string>>(inventory.RunningServices) ?? new(),
                CollectedAt = inventory.CollectedAt
            }).ToList();

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving inventory history for agent {AgentId}", agentId);
            return StatusCode(500, new { message = _configuration["Messages:ErrorOccurred"] ?? "An error occurred" });
        }
    }
}
