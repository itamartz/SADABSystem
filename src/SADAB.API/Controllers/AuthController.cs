using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SADAB.API.Data;
using SADAB.API.Services;
using SADAB.Shared.DTOs;

namespace SADAB.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ITokenService tokenService,
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        try
        {
            // Check if user already exists
            var existingUser = await _userManager.FindByNameAsync(request.Username);
            if (existingUser != null)
            {
                return BadRequest(new { message = _configuration["Messages:UserNameExists"] });
            }

            var existingEmail = await _userManager.FindByEmailAsync(request.Email);
            if (existingEmail != null)
            {
                return BadRequest(new { message = _configuration["Messages:EmailExists"] });
            }

            // Create new user
            var user = new ApplicationUser
            {
                UserName = request.Username,
                Email = request.Email,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, request.Password);

            if (!result.Succeeded)
            {
                return BadRequest(new { message = _configuration["Messages:UserCreationFailed"], errors = result.Errors });
            }

            _logger.LogInformation("User {Username} registered successfully", request.Username);

            // Generate JWT token
            var token = _tokenService.GenerateJwtToken(user);
            var jwtExpiration = _configuration.GetValue<int>("JwtSettings:ExpirationHours");
            var expiresAt = DateTime.UtcNow.AddHours(jwtExpiration);

            return Ok(new AuthResponse
            {
                Token = token,
                Username = user.UserName!,
                Email = user.Email!,
                ExpiresAt = expiresAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration for {Username}", request.Username);
            return StatusCode(500, new { message = _configuration["Messages:RegistrationError"] });
        }
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            var user = await _userManager.FindByNameAsync(request.Username);
            if (user == null)
            {
                return Unauthorized(new { message = _configuration["Messages:InvalidCredentials"] });
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: false);

            if (!result.Succeeded)
            {
                return Unauthorized(new { message = _configuration["Messages:InvalidCredentials"] });
            }

            // Update last login
            user.LastLoginAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            _logger.LogInformation("User {Username} logged in successfully", request.Username);

            // Generate JWT token
            var token = _tokenService.GenerateJwtToken(user);
            var jwtExpiration = _configuration.GetValue<int>("JwtSettings:ExpirationHours");
            var expiresAt = DateTime.UtcNow.AddHours(jwtExpiration);

            return Ok(new AuthResponse
            {
                Token = token,
                Username = user.UserName!,
                Email = user.Email!,
                ExpiresAt = expiresAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for {Username}", request.Username);
            return StatusCode(500, new { message = _configuration["Messages:LoginError"] });
        }
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult> GetCurrentUser()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return NotFound();
        }

        return Ok(new
        {
            user.Id,
            user.UserName,
            user.Email,
            user.CreatedAt,
            user.LastLoginAt
        });
    }
}
