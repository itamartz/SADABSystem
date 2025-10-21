using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SADAB.Server.Data;
using SADAB.Server.Models;
using SADAB.Shared.DTOs;
using System.Text.Json;

namespace SADAB.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InventoryController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<InventoryController> _logger;

    public InventoryController(ApplicationDbContext context, ILogger<InventoryController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> SubmitInventory([FromBody] InventoryDataDto inventoryDto)
    {
        try
        {
            var agentIdClaim = User.FindFirst("AgentId")?.Value;
            if (string.IsNullOrEmpty(agentIdClaim) || !Guid.TryParse(agentIdClaim, out var agentId))
            {
                return Unauthorized();
            }

            if (inventoryDto.AgentId != agentId)
            {
                return Forbid();
            }

            var inventory = new InventoryData
            {
                Id = Guid.NewGuid(),
                AgentId = agentId,
                HardwareInfo = JsonSerializer.Serialize(inventoryDto.HardwareInfo),
                InstalledSoftware = JsonSerializer.Serialize(inventoryDto.InstalledSoftware),
                EnvironmentVariables = JsonSerializer.Serialize(inventoryDto.EnvironmentVariables),
                RunningServices = JsonSerializer.Serialize(inventoryDto.RunningServices),
                CollectedAt = DateTime.UtcNow
            };

            _context.InventoryData.Add(inventory);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Inventory data received from agent {AgentId}", agentId);

            return Ok(new { message = "Inventory data received" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting inventory data");
            return StatusCode(500, new { message = "An error occurred" });
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
            return StatusCode(500, new { message = "An error occurred" });
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
            return StatusCode(500, new { message = "An error occurred" });
        }
    }
}
