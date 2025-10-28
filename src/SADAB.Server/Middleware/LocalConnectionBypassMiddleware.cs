using Microsoft.AspNetCore.Http;
using System.Net;
using System.Security.Claims;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace SADAB.Server.Middleware;

/// <summary>
/// Middleware that automatically authenticates requests from localhost,
/// bypassing normal authentication for local web app connections
/// while maintaining security for external API access.
/// </summary>
public class LocalConnectionBypassMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LocalConnectionBypassMiddleware> _logger;
    private readonly bool _enableLocalBypass;

    public LocalConnectionBypassMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<LocalConnectionBypassMiddleware> logger)
    {
        _next = next;
        _configuration = configuration;
        _logger = logger;

        // Read from config with default to true for development
        _enableLocalBypass = configuration.GetValue<bool>("SecuritySettings:EnableLocalBypass", true);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only bypass if enabled in configuration
        if (!_enableLocalBypass)
        {
            _logger.LogDebug("Local connection bypass is disabled");
            await _next(context);
            return;
        }

        // Only apply to /api/* endpoints
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        // Skip bypass for agent-specific endpoints (let certificate auth handle them)
        if (IsAgentEndpoint(context.Request.Path))
        {
            _logger.LogDebug("Skipping bypass for agent endpoint: {Path}", context.Request.Path);
            await _next(context);
            return;
        }

        // Check if request is from localhost
        var remoteIp = context.Connection.RemoteIpAddress;
        if (IsLocalConnection(remoteIp))
        {
            _logger.LogInformation("Local connection detected from {IP} to {Path}, bypassing authentication",
                remoteIp, context.Request.Path);

            // Create claims for local web app user
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, "LocalWebApp"),
                new Claim(ClaimTypes.Role, "WebApp"),
                new Claim("ConnectionType", "Local")
            };

            var identity = new ClaimsIdentity(claims, "LocalBypass");
            context.User = new ClaimsPrincipal(identity);

            _logger.LogDebug("Local bypass applied. User authenticated as LocalWebApp");
        }
        else
        {
            _logger.LogDebug("Remote connection from {IP}, normal authentication required", remoteIp);
        }

        await _next(context);
    }

    /// <summary>
    /// Checks if the IP address is from localhost
    /// </summary>
    private bool IsLocalConnection(IPAddress? remoteIp)
    {
        if (remoteIp == null)
            return false;

        // Check for IPv4 localhost
        if (IPAddress.IsLoopback(remoteIp))
            return true;

        // Check for IPv6 localhost
        if (remoteIp.Equals(IPAddress.IPv6Loopback))
            return true;

        // Check if it's the same as local IP
        var localIp = Dns.GetHostEntry(Dns.GetHostName())
            .AddressList
            .FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

        if (localIp != null && remoteIp.Equals(localIp))
            return true;

        return false;
    }

    /// <summary>
    /// Determines if the endpoint is agent-specific (should use certificate auth)
    /// </summary>
    private bool IsAgentEndpoint(PathString path)
    {
        var agentPaths = new[]
        {
            "/api/agents/register",
            "/api/agents/heartbeat",
            "/api/agents/refresh-certificate",
            "/api/deployments/pending",
            "/api/deployments/files",
            "/api/commands/pending"
        };

        return agentPaths.Any(p => path.StartsWithSegments(p));
    }
}

public static class LocalConnectionBypassMiddlewareExtensions
{
    public static IApplicationBuilder UseLocalConnectionBypass(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<LocalConnectionBypassMiddleware>();
    }
}