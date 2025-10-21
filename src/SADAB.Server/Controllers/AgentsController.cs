using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SADAB.Server.Data;
using SADAB.Server.Models;
using SADAB.Server.Services;
using SADAB.Shared.DTOs;
using SADAB.Shared.Enums;
using System.Text.Json;

namespace SADAB.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ICertificateService _certificateService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AgentsController> _logger;

    public AgentsController(
        ApplicationDbContext context,
        ICertificateService certificateService,
        IConfiguration configuration,
        ILogger<AgentsController> logger)
    {
        _context = context;
        _certificateService = certificateService;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AgentRegistrationResponse>> Register([FromBody] AgentRegistrationRequest request)
    {
        try
        {
            // Check if agent with same MachineId already exists
            var existingAgent = await _context.Agents
                .FirstOrDefaultAsync(a => a.MachineId == request.MachineId);

            Agent agent;

            if (existingAgent != null)
            {
                // Update existing agent
                agent = existingAgent;
                agent.MachineName = request.MachineName;
                agent.OperatingSystem = request.OperatingSystem;
                agent.IpAddress = request.IpAddress;
                agent.Status = AgentStatus.Online;
                agent.LastHeartbeat = DateTime.UtcNow;

                if (request.Metadata != null)
                {
                    agent.Metadata = JsonSerializer.Serialize(request.Metadata);
                }

                _logger.LogInformation("Agent {MachineId} re-registered", request.MachineId);
            }
            else
            {
                // Create new agent
                agent = new Agent
                {
                    Id = Guid.NewGuid(),
                    MachineName = request.MachineName,
                    MachineId = request.MachineId,
                    OperatingSystem = request.OperatingSystem,
                    IpAddress = request.IpAddress,
                    Status = AgentStatus.Online,
                    LastHeartbeat = DateTime.UtcNow,
                    RegisteredAt = DateTime.UtcNow,
                    Metadata = request.Metadata != null ? JsonSerializer.Serialize(request.Metadata) : null
                };

                _context.Agents.Add(agent);
                _logger.LogInformation("New agent {MachineId} registered", request.MachineId);
            }

            await _context.SaveChangesAsync();

            // Generate certificate
            var (certificate, privateKey, expiresAt) = await _certificateService.GenerateCertificateAsync(
                agent.Id, agent.MachineName);

            // Update agent with certificate info
            var cert = await _context.AgentCertificates
                .Where(c => c.AgentId == agent.Id)
                .OrderByDescending(c => c.IssuedAt)
                .FirstOrDefaultAsync();

            if (cert != null)
            {
                agent.CurrentCertificateThumbprint = cert.Thumbprint;
                agent.CertificateExpiresAt = cert.ExpiresAt;
                await _context.SaveChangesAsync();
            }

            return Ok(new AgentRegistrationResponse
            {
                AgentId = agent.Id,
                Certificate = certificate,
                PrivateKey = privateKey,
                ExpiresAt = expiresAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering agent {MachineId}", request.MachineId);
            return StatusCode(500, new { message = _configuration["Messages:RegistrationError"] ?? "An error occurred during registration" });
        }
    }

    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat([FromBody] AgentHeartbeatRequest request)
    {
        try
        {
            var agentIdClaim = User.FindFirst("AgentId")?.Value;
            if (string.IsNullOrEmpty(agentIdClaim) || !Guid.TryParse(agentIdClaim, out var agentId))
            {
                return Unauthorized();
            }

            var agent = await _context.Agents.FindAsync(agentId);
            if (agent == null)
            {
                return NotFound();
            }

            agent.Status = request.Status;
            agent.LastHeartbeat = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(request.IpAddress))
            {
                agent.IpAddress = request.IpAddress;
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = _configuration["Messages:HeartbeatReceived"] ?? "Heartbeat received" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing heartbeat");
            return StatusCode(500, new { message = _configuration["Messages:ErrorOccurred"] ?? "An error occurred" });
        }
    }

    [HttpPost("refresh-certificate")]
    public async Task<ActionResult<CertificateRefreshResponse>> RefreshCertificate(
        [FromBody] CertificateRefreshRequest request)
    {
        try
        {
            var agentIdClaim = User.FindFirst("AgentId")?.Value;
            if (string.IsNullOrEmpty(agentIdClaim) || !Guid.TryParse(agentIdClaim, out var agentId))
            {
                return Unauthorized();
            }

            if (request.AgentId != agentId)
            {
                return Forbid();
            }

            var agent = await _context.Agents.FindAsync(agentId);
            if (agent == null)
            {
                return NotFound();
            }

            // Validate current certificate
            var isValid = await _certificateService.ValidateCertificateAsync(request.CurrentCertificateThumbprint);
            if (!isValid)
            {
                return BadRequest(new { message = _configuration["Messages:CertificateInvalid"] ?? "Current certificate is invalid" });
            }

            // Generate new certificate
            var (certificate, privateKey, expiresAt) = await _certificateService.GenerateCertificateAsync(
                agent.Id, agent.MachineName);

            // Update agent with new certificate info
            var cert = await _context.AgentCertificates
                .Where(c => c.AgentId == agent.Id)
                .OrderByDescending(c => c.IssuedAt)
                .FirstOrDefaultAsync();

            if (cert != null)
            {
                agent.CurrentCertificateThumbprint = cert.Thumbprint;
                agent.CertificateExpiresAt = cert.ExpiresAt;
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("Certificate refreshed for agent {AgentId}", agentId);

            return Ok(new CertificateRefreshResponse
            {
                Certificate = certificate,
                PrivateKey = privateKey,
                ExpiresAt = expiresAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing certificate for agent {AgentId}", request.AgentId);
            return StatusCode(500, new { message = _configuration["Messages:ErrorOccurred"] ?? "An error occurred" });
        }
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<List<AgentDto>>> GetAllAgents()
    {
        try
        {
            var agents = await _context.Agents
                .OrderByDescending(a => a.LastHeartbeat)
                .Select(a => new AgentDto
                {
                    Id = a.Id,
                    MachineName = a.MachineName,
                    MachineId = a.MachineId,
                    OperatingSystem = a.OperatingSystem,
                    IpAddress = a.IpAddress,
                    Status = a.Status,
                    LastHeartbeat = a.LastHeartbeat,
                    RegisteredAt = a.RegisteredAt,
                    CertificateExpiresAt = a.CertificateExpiresAt
                })
                .ToListAsync();

            return Ok(agents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving agents");
            return StatusCode(500, new { message = _configuration["Messages:ErrorOccurred"] ?? "An error occurred" });
        }
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<AgentDto>> GetAgent(Guid id)
    {
        try
        {
            var agent = await _context.Agents.FindAsync(id);
            if (agent == null)
            {
                return NotFound();
            }

            return Ok(new AgentDto
            {
                Id = agent.Id,
                MachineName = agent.MachineName,
                MachineId = agent.MachineId,
                OperatingSystem = agent.OperatingSystem,
                IpAddress = agent.IpAddress,
                Status = agent.Status,
                LastHeartbeat = agent.LastHeartbeat,
                RegisteredAt = agent.RegisteredAt,
                CertificateExpiresAt = agent.CertificateExpiresAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving agent {AgentId}", id);
            return StatusCode(500, new { message = _configuration["Messages:ErrorOccurred"] ?? "An error occurred" });
        }
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteAgent(Guid id)
    {
        try
        {
            var agent = await _context.Agents.FindAsync(id);
            if (agent == null)
            {
                return NotFound();
            }

            _context.Agents.Remove(agent);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Agent {AgentId} deleted", id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting agent {AgentId}", id);
            return StatusCode(500, new { message = _configuration["Messages:ErrorOccurred"] ?? "An error occurred" });
        }
    }
}
