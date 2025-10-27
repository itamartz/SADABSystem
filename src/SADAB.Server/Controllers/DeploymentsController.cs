using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SADAB.Server.Data;
using SADAB.Server.Models;
using SADAB.Shared.DTOs;
using SADAB.Shared.Enums;
using System.Security.Claims;

namespace SADAB.Server.Controllers;

/// <summary>
/// Manages deployment operations including creation, retrieval, and execution tracking
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class DeploymentsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DeploymentsController> _logger;

    public DeploymentsController(
        ApplicationDbContext context,
        IConfiguration configuration,
        ILogger<DeploymentsController> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new deployment with specified configuration and target agents
    /// </summary>
    /// <param name="request">Deployment configuration including package, targets, and success exit codes</param>
    /// <returns>The created deployment details</returns>
    /// <response code="200">Deployment created successfully</response>
    /// <response code="400">Invalid request or deployment folder not found</response>
    /// <response code="401">Unauthorized - valid authentication required</response>
    /// <response code="500">Internal server error occurred</response>
    [HttpPost]
    [ProducesResponseType(typeof(DeploymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DeploymentDto>> CreateDeployment([FromBody] CreateDeploymentRequest request)
    {
        try
        {
            var userName = User.FindFirst(ClaimTypes.Name)?.Value;

            // Validate deployment folder exists
            var deploymentsPath = _configuration["DeploymentsPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "Deployments");
            var packagePath = Path.Combine(deploymentsPath, request.PackageFolderName);

            if (!Directory.Exists(packagePath))
            {
                var errorMessage = _configuration["Messages:DeploymentFolderNotFound"] ?? "Deployment folder '{0}' does not exist";
                return BadRequest(new { message = string.Format(errorMessage, request.PackageFolderName) });
            }

            var deployment = new Deployment
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Description = request.Description,
                Type = request.Type,
                PackageFolderName = request.PackageFolderName,
                ExecutablePath = request.ExecutablePath,
                Arguments = request.Arguments,
                RunAsAdmin = request.RunAsAdmin,
                TimeoutMinutes = request.TimeoutMinutes,
                SuccessExitCodes = request.SuccessExitCodes,
                Status = DeploymentStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userName
            };

            _context.Deployments.Add(deployment);

            // Add target agents
            if (request.TargetAgentIds != null && request.TargetAgentIds.Any())
            {
                foreach (var agentId in request.TargetAgentIds)
                {
                    var agent = await _context.Agents.FindAsync(agentId);
                    if (agent != null)
                    {
                        _context.DeploymentTargets.Add(new DeploymentTarget
                        {
                            Id = Guid.NewGuid(),
                            DeploymentId = deployment.Id,
                            AgentId = agentId,
                            AddedAt = DateTime.UtcNow
                        });

                        // Create pending result
                        _context.DeploymentResults.Add(new DeploymentResult
                        {
                            Id = Guid.NewGuid(),
                            DeploymentId = deployment.Id,
                            AgentId = agentId,
                            Status = DeploymentStatus.Pending,
                            StartedAt = DateTime.UtcNow
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Deployment {DeploymentId} created by {User}", deployment.Id, userName);

            return Ok(await GetDeploymentDto(deployment.Id));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating deployment");
            return StatusCode(500, new { message = _configuration["Messages:ErrorOccurred"] ?? "An error occurred" });
        }
    }

    /// <summary>
    /// Retrieves all deployments with their status and result counts
    /// </summary>
    /// <returns>List of all deployments</returns>
    /// <response code="200">Returns the list of deployments</response>
    /// <response code="401">Unauthorized - valid authentication required</response>
    /// <response code="500">Internal server error occurred</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<DeploymentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<DeploymentDto>>> GetAllDeployments()
    {
        try
        {
            var deployments = await _context.Deployments
                .Include(d => d.Targets)
                .Include(d => d.Results)
                .OrderByDescending(d => d.CreatedAt)
                .Select(d => new DeploymentDto
                {
                    Id = d.Id,
                    Name = d.Name,
                    Description = d.Description,
                    Type = d.Type,
                    PackageFolderName = d.PackageFolderName,
                    Status = d.Status,
                    CreatedAt = d.CreatedAt,
                    CreatedBy = d.CreatedBy,
                    TargetAgentCount = d.Targets.Count,
                    SuccessCount = d.Results.Count(r => r.Status == DeploymentStatus.Completed),
                    FailedCount = d.Results.Count(r => r.Status == DeploymentStatus.Failed)
                })
                .ToListAsync();

            return Ok(deployments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving deployments");
            return StatusCode(500, new { message = _configuration["Messages:ErrorOccurred"] ?? "An error occurred" });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<DeploymentDto>> GetDeployment(Guid id)
    {
        try
        {
            var dto = await GetDeploymentDto(id);
            if (dto == null)
            {
                return NotFound();
            }

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving deployment {DeploymentId}", id);
            return StatusCode(500, new { message = _configuration["Messages:ErrorOccurred"] ?? "An error occurred" });
        }
    }

    [HttpGet("{id}/results")]
    public async Task<ActionResult<List<DeploymentResultDto>>> GetDeploymentResults(Guid id)
    {
        try
        {
            var results = await _context.DeploymentResults
                .Include(r => r.Agent)
                .Where(r => r.DeploymentId == id)
                .Select(r => new DeploymentResultDto
                {
                    Id = r.Id,
                    DeploymentId = r.DeploymentId,
                    AgentId = r.AgentId,
                    AgentName = r.Agent.MachineName,
                    Status = r.Status,
                    StartedAt = r.StartedAt,
                    CompletedAt = r.CompletedAt,
                    ExitCode = r.ExitCode,
                    Output = r.Output,
                    ErrorMessage = r.ErrorMessage
                })
                .ToListAsync();

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving deployment results for {DeploymentId}", id);
            return StatusCode(500, new { message = _configuration["Messages:ErrorOccurred"] ?? "An error occurred" });
        }
    }

    /// <summary>
    /// Retrieves pending deployments for the authenticated agent
    /// </summary>
    /// <remarks>
    /// This endpoint is called by agents to check for new deployments.
    /// Requires certificate authentication (X-Client-Certificate-Thumbprint header).
    /// Automatically updates deployment status from Pending to InProgress.
    /// </remarks>
    /// <returns>List of pending deployment tasks with file information</returns>
    /// <response code="200">Returns pending deployments for this agent</response>
    /// <response code="401">Unauthorized - valid agent certificate required</response>
    /// <response code="500">Internal server error occurred</response>
    [HttpGet("pending")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<DeploymentTaskDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<DeploymentTaskDto>>> GetPendingDeployments()
    {
        try
        {
            _logger.LogDebug("GetPendingDeployments called");
            _logger.LogDebug("User.Identity.IsAuthenticated: {IsAuthenticated}", User.Identity?.IsAuthenticated ?? false);
            _logger.LogDebug("User.Identity.AuthenticationType: {AuthType}", User.Identity?.AuthenticationType ?? "(null)");
            _logger.LogDebug("User.Claims count: {ClaimCount}", User.Claims.Count());

            foreach (var claim in User.Claims)
            {
                _logger.LogDebug("Claim: {Type} = {Value}", claim.Type, claim.Value);
            }

            var agentIdClaim = User.FindFirst("AgentId")?.Value;
            _logger.LogDebug("AgentId claim value: {AgentIdClaim}", agentIdClaim ?? "(null)");

            if (string.IsNullOrEmpty(agentIdClaim) || !Guid.TryParse(agentIdClaim, out var agentId))
            {
                _logger.LogWarning("Unauthorized: AgentId claim is null or invalid");
                return Unauthorized();
            }

            _logger.LogInformation("Processing pending deployments for AgentId: {AgentId}", agentId);

            var deploymentsPath = _configuration["DeploymentsPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "Deployments");

            var pendingResults = await _context.DeploymentResults
                .Include(r => r.Deployment)
                .Where(r => r.AgentId == agentId && r.Status == DeploymentStatus.Pending)
                .ToListAsync();

            var tasks = new List<DeploymentTaskDto>();

            foreach (var result in pendingResults)
            {
                var packagePath = Path.Combine(deploymentsPath, result.Deployment.PackageFolderName);
                var files = new List<string>();

                if (Directory.Exists(packagePath))
                {
                    files = Directory.GetFiles(packagePath, "*", SearchOption.AllDirectories)
                        .Select(f => Path.GetRelativePath(packagePath, f))
                        .ToList();
                }

                tasks.Add(new DeploymentTaskDto
                {
                    DeploymentId = result.DeploymentId,
                    Name = result.Deployment.Name,
                    Type = result.Deployment.Type,
                    Files = files,
                    ExecutablePath = result.Deployment.ExecutablePath,
                    Arguments = result.Deployment.Arguments,
                    RunAsAdmin = result.Deployment.RunAsAdmin,
                    TimeoutMinutes = result.Deployment.TimeoutMinutes,
                    SuccessExitCodes = result.Deployment.SuccessExitCodes
                });

                // Update status to InProgress
                result.Status = DeploymentStatus.InProgress;
            }

            await _context.SaveChangesAsync();

            return Ok(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending deployments");
            return StatusCode(500, new { message = _configuration["Messages:ErrorOccurred"] ?? "An error occurred" });
        }
    }

    [HttpPost("{deploymentId}/results")]
    [AllowAnonymous]
    public async Task<IActionResult> UpdateDeploymentResult(Guid deploymentId, [FromBody] DeploymentResultDto resultDto)
    {
        try
        {
            var agentIdClaim = User.FindFirst("AgentId")?.Value;
            if (string.IsNullOrEmpty(agentIdClaim) || !Guid.TryParse(agentIdClaim, out var agentId))
            {
                return Unauthorized();
            }

            var result = await _context.DeploymentResults
                .FirstOrDefaultAsync(r => r.DeploymentId == deploymentId && r.AgentId == agentId);

            if (result == null)
            {
                return NotFound();
            }

            result.Status = resultDto.Status;
            result.CompletedAt = resultDto.CompletedAt;
            result.ExitCode = resultDto.ExitCode;
            result.Output = resultDto.Output;
            result.ErrorMessage = resultDto.ErrorMessage;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Deployment result updated for deployment {DeploymentId}, agent {AgentId}", deploymentId, agentId);

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating deployment result");
            return StatusCode(500, new { message = _configuration["Messages:ErrorOccurred"] ?? "An error occurred" });
        }
    }

    [HttpGet("files/{deploymentId}")]
    [AllowAnonymous]
    public async Task<IActionResult> DownloadDeploymentFile(Guid deploymentId, [FromQuery] string filePath)
    {
        try
        {
            var agentIdClaim = User.FindFirst("AgentId")?.Value;
            if (string.IsNullOrEmpty(agentIdClaim) || !Guid.TryParse(agentIdClaim, out var agentId))
            {
                return Unauthorized();
            }

            var deployment = await _context.Deployments.FindAsync(deploymentId);
            if (deployment == null)
            {
                return NotFound();
            }

            var deploymentsPath = _configuration["DeploymentsPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "Deployments");
            var fullPath = Path.Combine(deploymentsPath, deployment.PackageFolderName, filePath);

            if (!System.IO.File.Exists(fullPath))
            {
                return NotFound();
            }

            var fileBytes = await System.IO.File.ReadAllBytesAsync(fullPath);
            return File(fileBytes, "application/octet-stream", Path.GetFileName(filePath));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading deployment file");
            return StatusCode(500, new { message = _configuration["Messages:ErrorOccurred"] ?? "An error occurred" });
        }
    }

    private async Task<DeploymentDto?> GetDeploymentDto(Guid id)
    {
        return await _context.Deployments
            .Include(d => d.Targets)
            .Include(d => d.Results)
            .Where(d => d.Id == id)
            .Select(d => new DeploymentDto
            {
                Id = d.Id,
                Name = d.Name,
                Description = d.Description,
                Type = d.Type,
                PackageFolderName = d.PackageFolderName,
                Status = d.Status,
                CreatedAt = d.CreatedAt,
                CreatedBy = d.CreatedBy,
                TargetAgentCount = d.Targets.Count,
                SuccessCount = d.Results.Count(r => r.Status == DeploymentStatus.Completed),
                FailedCount = d.Results.Count(r => r.Status == DeploymentStatus.Failed)
            })
            .FirstOrDefaultAsync();
    }
}
