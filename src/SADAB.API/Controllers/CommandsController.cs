using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SADAB.API.Data;
using SADAB.API.Models;
using SADAB.Shared.DTOs;
using SADAB.Shared.Enums;
using System.Security.Claims;

namespace SADAB.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CommandsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CommandsController> _logger;

    public CommandsController(ApplicationDbContext context, IConfiguration configuration, ILogger<CommandsController> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("execute")]
    public async Task<ActionResult<List<CommandExecutionDto>>> ExecuteCommand([FromBody] ExecuteCommandRequest request)
    {
        try
        {
            var userName = User.FindFirst(ClaimTypes.Name)?.Value;
            var executions = new List<CommandExecution>();

            if (request.TargetAgentIds == null || !request.TargetAgentIds.Any())
            {
                return BadRequest(new { message = _configuration["Messages:NoTargetAgents"] ?? "No target agents specified" });
            }

            foreach (var agentId in request.TargetAgentIds)
            {
                var agent = await _context.Agents.FindAsync(agentId);
                if (agent == null) continue;

                var execution = new CommandExecution
                {
                    Id = Guid.NewGuid(),
                    AgentId = agentId,
                    Command = request.Command,
                    Arguments = request.Arguments,
                    RunAsAdmin = request.RunAsAdmin,
                    TimeoutMinutes = request.TimeoutMinutes,
                    Status = CommandExecutionStatus.Pending,
                    RequestedAt = DateTime.Now,
                    RequestedBy = userName
                };

                _context.CommandExecutions.Add(execution);
                executions.Add(execution);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Command execution requested for {Count} agents by {User}", executions.Count, userName);

            return Ok(executions.Select(e => new CommandExecutionDto
            {
                Id = e.Id,
                AgentId = e.AgentId,
                Command = e.Command,
                Arguments = e.Arguments,
                Status = e.Status,
                RequestedAt = e.RequestedAt
            }).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command");
            return StatusCode(500, new { message = _configuration["Messages:ErrorOccurred"] ?? "An error occurred" });
        }
    }

    [HttpGet]
    public async Task<ActionResult<List<CommandExecutionDto>>> GetAllCommands()
    {
        try
        {
            var commands = await _context.CommandExecutions
                .Include(c => c.Agent)
                .OrderByDescending(c => c.RequestedAt)
                .Select(c => new CommandExecutionDto
                {
                    Id = c.Id,
                    AgentId = c.AgentId,
                    AgentName = c.Agent.MachineName,
                    Command = c.Command,
                    Arguments = c.Arguments,
                    Status = c.Status,
                    RequestedAt = c.RequestedAt,
                    StartedAt = c.StartedAt,
                    CompletedAt = c.CompletedAt,
                    ExitCode = c.ExitCode,
                    Output = c.Output,
                    ErrorOutput = c.ErrorOutput
                })
                .ToListAsync();

            return Ok(commands);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving commands");
            return StatusCode(500, new { message = _configuration["Messages:ErrorOccurred"] ?? "An error occurred" });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CommandExecutionDto>> GetCommand(Guid id)
    {
        try
        {
            var command = await _context.CommandExecutions
                .Include(c => c.Agent)
                .Where(c => c.Id == id)
                .Select(c => new CommandExecutionDto
                {
                    Id = c.Id,
                    AgentId = c.AgentId,
                    AgentName = c.Agent.MachineName,
                    Command = c.Command,
                    Arguments = c.Arguments,
                    Status = c.Status,
                    RequestedAt = c.RequestedAt,
                    StartedAt = c.StartedAt,
                    CompletedAt = c.CompletedAt,
                    ExitCode = c.ExitCode,
                    Output = c.Output,
                    ErrorOutput = c.ErrorOutput
                })
                .FirstOrDefaultAsync();

            if (command == null)
            {
                return NotFound();
            }

            return Ok(command);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving command {CommandId}", id);
            return StatusCode(500, new { message = _configuration["Messages:ErrorOccurred"] ?? "An error occurred" });
        }
    }

    [HttpGet("pending")]
    [AllowAnonymous]
    public async Task<ActionResult<List<CommandExecutionDto>>> GetPendingCommands()
    {
        try
        {
            var agentIdClaim = User.FindFirst("AgentId")?.Value;
            if (string.IsNullOrEmpty(agentIdClaim) || !Guid.TryParse(agentIdClaim, out var agentId))
            {
                return Unauthorized();
            }

            var commands = await _context.CommandExecutions
                .Where(c => c.AgentId == agentId && c.Status == CommandExecutionStatus.Pending)
                .Select(c => new CommandExecutionDto
                {
                    Id = c.Id,
                    AgentId = c.AgentId,
                    Command = c.Command,
                    Arguments = c.Arguments,
                    Status = c.Status,
                    RequestedAt = c.RequestedAt
                })
                .ToListAsync();

            // Update status to Running
            foreach (var cmd in commands)
            {
                var execution = await _context.CommandExecutions.FindAsync(cmd.Id);
                if (execution != null)
                {
                    execution.Status = CommandExecutionStatus.Running;
                    execution.StartedAt = DateTime.Now;
                }
            }

            await _context.SaveChangesAsync();

            return Ok(commands);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending commands");
            return StatusCode(500, new { message = _configuration["Messages:ErrorOccurred"] ?? "An error occurred" });
        }
    }

    [HttpPost("{id}/result")]
    [AllowAnonymous]
    public async Task<IActionResult> UpdateCommandResult(Guid id, [FromBody] CommandExecutionDto resultDto)
    {
        try
        {
            var agentIdClaim = User.FindFirst("AgentId")?.Value;
            if (string.IsNullOrEmpty(agentIdClaim) || !Guid.TryParse(agentIdClaim, out var agentId))
            {
                return Unauthorized();
            }

            var execution = await _context.CommandExecutions.FindAsync(id);
            if (execution == null || execution.AgentId != agentId)
            {
                return NotFound();
            }

            execution.Status = resultDto.Status;
            execution.CompletedAt = resultDto.CompletedAt ?? DateTime.Now;
            execution.ExitCode = resultDto.ExitCode;
            execution.Output = resultDto.Output;
            execution.ErrorOutput = resultDto.ErrorOutput;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Command result updated for command {CommandId}, agent {AgentId}", id, agentId);

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating command result");
            return StatusCode(500, new { message = _configuration["Messages:ErrorOccurred"] ?? "An error occurred" });
        }
    }
}
