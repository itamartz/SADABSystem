using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SADAB.API.Data;
using SADAB.API.Models;
using SADAB.API.Services;
using SADAB.Shared.DTOs;
using SADAB.Shared.Enums;
using System.Text.Json;

namespace SADAB.API.Controllers;

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
                _logger.LogWarning("Unauthorized heartbeat attempt");
                return Unauthorized();
            }

            var agent = await _context.Agents.FindAsync(agentId);
            if (agent == null)
            {
                _logger.LogWarning("Heartbeat received for unknown agent {AgentId}", agentId);
                return NotFound();
            }
            _logger.LogInformation("Heartbeat received from agent {AgentId} with status {Status}", agentId, request.Status);

            agent.Status = request.Status;
            agent.LastHeartbeat = DateTime.Now;
            _logger.LogInformation("Updated agent {AgentId} status to {Status}", agentId, request.Status);

            if (!string.IsNullOrEmpty(request.IpAddress))
            {
                agent.IpAddress = request.IpAddress;
            }

            // Update OperatingSystem and SystemInfo from request
            if (request.SystemInfo != null)
            {
                // Update OS Version
                if (request.SystemInfo.ContainsKey("OSVersion"))
                {
                    var osVersion = request.SystemInfo["OSVersion"]?.ToString();
                    if (!string.IsNullOrEmpty(osVersion))
                    {
                        agent.OperatingSystem = osVersion;
                        _logger.LogDebug("Updated agent {AgentId} OS version to {OSVersion}", agentId, osVersion);
                    }
                }

                // Store entire SystemInfo in Metadata JSON field
                agent.Metadata = JsonSerializer.Serialize(request.SystemInfo);
                _logger.LogDebug("Updated agent {AgentId} metadata with SystemInfo", agentId);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Agent {agent} heartbeat processed successfully", agent);
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
                _logger.LogWarning("Unauthorized certificate refresh attempt");
                return Unauthorized();
            }

            if (request.AgentId != agentId)
            {
                _logger.LogWarning("Agent {AgentId} attempted to refresh certificate for agent {RequestAgentId}", agentId, request.AgentId);
                return Forbid();
            }

            var agent = await _context.Agents.FindAsync(agentId);
            if (agent == null)
            {
                _logger.LogWarning("Certificate refresh requested for unknown agent {RequestAgentId}", request.AgentId);
                return NotFound();
            }

            // Validate current certificate
            var isValid = await _certificateService.ValidateCertificateAsync(request.CurrentCertificateThumbprint);
            if (!isValid)
            {
                _logger.LogWarning("Invalid certificate thumbprint provided by agent {agent}", agent.MachineName);
                return BadRequest(new { message = _configuration["Messages:CertificateInvalid"] ?? "Current certificate is invalid" });
            }

            // Generate new certificate
            _logger.LogInformation("Refreshing the certificate for agent {agent}", agent.MachineName);
            var (certificate, privateKey, expiresAt) = await _certificateService.GenerateCertificateAsync(
                agent.Id, agent.MachineName);

            // Update agent with new certificate info
            var cert = await _context.AgentCertificates
                .Where(c => c.AgentId == agent.Id)
                .OrderByDescending(c => c.IssuedAt)
                .FirstOrDefaultAsync();
            _logger.LogInformation("New certificate generated for agent {agent}", agent.MachineName);

            if (cert != null)
            {
                agent.CurrentCertificateThumbprint = cert.Thumbprint;
                agent.CertificateExpiresAt = cert.ExpiresAt;
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("Certificate refreshed for agent {agent}", agent);

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
                .ToListAsync();

            var agentDtos = agents.Select(a =>
            {
                double? cpuUsage = null;
                double? memoryUsage = null;

                // Extract CPU and Memory from Metadata JSON
                if (!string.IsNullOrEmpty(a.Metadata))
                {
                    try
                    {
                        var metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(a.Metadata);
                        if (metadata != null)
                        {
                            if (metadata.ContainsKey("CpuUsagePercent"))
                            {
                                var cpuValue = metadata["CpuUsagePercent"];
                                if (cpuValue != null && double.TryParse(cpuValue.ToString(), out var cpu) && cpu >= 0)
                                {
                                    cpuUsage = cpu;
                                }
                            }

                            if (metadata.ContainsKey("MemoryUsagePercent"))
                            {
                                var memValue = metadata["MemoryUsagePercent"];
                                if (memValue != null && double.TryParse(memValue.ToString(), out var mem) && mem >= 0)
                                {
                                    memoryUsage = mem;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error parsing metadata for agent {AgentId}", a.Id);
                    }
                }

                return new AgentDto
                {
                    Id = a.Id,
                    MachineName = a.MachineName,
                    MachineId = a.MachineId,
                    OperatingSystem = a.OperatingSystem,
                    IpAddress = a.IpAddress,
                    Status = a.Status,
                    LastHeartbeat = a.LastHeartbeat,
                    RegisteredAt = a.RegisteredAt,
                    CertificateExpiresAt = a.CertificateExpiresAt,
                    CpuUsagePercent = cpuUsage,
                    MemoryUsagePercent = memoryUsage
                };
            }).ToList();

            return Ok(agentDtos);
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

            double? cpuUsage = null;
            double? memoryUsage = null;

            // Extract CPU and Memory from Metadata JSON
            if (!string.IsNullOrEmpty(agent.Metadata))
            {
                try
                {
                    var metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(agent.Metadata);
                    if (metadata != null)
                    {
                        if (metadata.ContainsKey("CpuUsagePercent"))
                        {
                            var cpuValue = metadata["CpuUsagePercent"];
                            if (cpuValue != null && double.TryParse(cpuValue.ToString(), out var cpu) && cpu >= 0)
                            {
                                cpuUsage = cpu;
                            }
                        }

                        if (metadata.ContainsKey("MemoryUsagePercent"))
                        {
                            var memValue = metadata["MemoryUsagePercent"];
                            if (memValue != null && double.TryParse(memValue.ToString(), out var mem) && mem >= 0)
                            {
                                memoryUsage = mem;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing metadata for agent {AgentId}", id);
                }
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
                CertificateExpiresAt = agent.CertificateExpiresAt,
                CpuUsagePercent = cpuUsage,
                MemoryUsagePercent = memoryUsage
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
