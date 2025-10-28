using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;
using SADAB.Server.Services;

namespace SADAB.Server.Middleware;

public class CertificateAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CertificateAuthenticationMiddleware> _logger;
    private readonly string _certificateHeaderName;
    //private readonly string _agentEndpointPath;
    private readonly string _registerEndpointPath;
    private readonly string _agentIdClaimType;
    private readonly string _certificateThumbprintClaimType;
    private readonly string _agentRole;
    private readonly string _certificateScheme;

    public CertificateAuthenticationMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<CertificateAuthenticationMiddleware> logger)
    {
        _next = next;
        _configuration = configuration;
        _logger = logger;

        _certificateHeaderName = _configuration["ServiceSettings:CertificateHeaderName"] ?? "X-Client-Certificate-Thumbprint";
        //_agentEndpointPath = "/api/agents";
        _registerEndpointPath = "/api/agents/register";
        _agentIdClaimType = "AgentId";
        _certificateThumbprintClaimType = "CertificateThumbprint";
        _agentRole = "Agent";
        _certificateScheme = "Certificate";
    }

    public async Task InvokeAsync(HttpContext context, ICertificateService certificateService)
    {
        // Skip authentication for registration endpoint
        if (context.Request.Path.StartsWithSegments(_registerEndpointPath))
        {
            _logger.LogDebug("Skipping authentication for registration endpoint: {Path}", context.Request.Path);
            await _next(context);
            return;
        }

        // Apply certificate authentication to ALL /api/* endpoints (secure by default)
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            _logger.LogDebug("Path {Path} is not an API endpoint, passing through", context.Request.Path);
            await _next(context);
            return;
        }

        _logger.LogDebug("Processing certificate authentication for API path: {Path}", context.Request.Path);

        var clientCertificate = context.Connection.ClientCertificate;

        if (clientCertificate == null)
        {
            // Try to get certificate from header (for development/testing)
            var certThumbprint = context.Request.Headers[_certificateHeaderName].FirstOrDefault();

            _logger.LogDebug("Processing request to {Path} - Certificate header value: {Thumbprint}",
                context.Request.Path, certThumbprint ?? "(null)");

            if (string.IsNullOrEmpty(certThumbprint))
            {
                _logger.LogWarning("No client certificate provided for agent endpoint");
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync(_configuration["Messages:CertificateRequired"] ?? "Client certificate required");
                return;
            }

            // Validate thumbprint from header
            _logger.LogDebug("Validating certificate thumbprint: {Thumbprint}", certThumbprint);
            var isValid = await certificateService.ValidateCertificateAsync(certThumbprint);
            _logger.LogDebug("Certificate validation result: {IsValid}", isValid);

            if (!isValid)
            {
                _logger.LogWarning("Invalid certificate thumbprint: {Thumbprint}", certThumbprint);
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync(_configuration["Messages:InvalidCertificate"] ?? "Invalid certificate");
                return;
            }

            _logger.LogDebug("Retrieving agent certificate by thumbprint: {Thumbprint}", certThumbprint);
            var agentCert = await certificateService.GetCertificateByThumbprintAsync(certThumbprint);

            if (agentCert != null)
            {
                _logger.LogInformation("Certificate found for AgentId: {AgentId}, setting claims", agentCert.AgentId);

                // Add agent claims
                var claims = new[]
                {
                    new Claim(_agentIdClaimType, agentCert.AgentId.ToString()),
                    new Claim(_certificateThumbprintClaimType, certThumbprint),
                    new Claim(ClaimTypes.Role, _agentRole)
                };

                var identity = new ClaimsIdentity(claims, _certificateScheme);
                context.User = new ClaimsPrincipal(identity);

                _logger.LogDebug("Claims set successfully. User.Identity.IsAuthenticated: {IsAuthenticated}",
                    context.User.Identity?.IsAuthenticated ?? false);
            }
            else
            {
                _logger.LogError("AgentCertificate NOT FOUND in database for thumbprint: {Thumbprint}", certThumbprint);
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync(_configuration["Messages:InvalidCertificate"] ?? "Invalid certificate");
                return;
            }
        }
        else
        {
            // Validate actual certificate
            var thumbprint = clientCertificate.Thumbprint;
            var isValid = await certificateService.ValidateCertificateAsync(thumbprint);

            if (!isValid)
            {
                _logger.LogWarning("Invalid client certificate: {Thumbprint}", thumbprint);
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync(_configuration["Messages:InvalidCertificate"] ?? "Invalid certificate");
                return;
            }

            var agentCert = await certificateService.GetCertificateByThumbprintAsync(thumbprint);
            if (agentCert != null)
            {
                // Add agent claims
                var claims = new[]
                {
                    new Claim(_agentIdClaimType, agentCert.AgentId.ToString()),
                    new Claim(_certificateThumbprintClaimType, thumbprint),
                    new Claim(ClaimTypes.Role, _agentRole)
                };

                var identity = new ClaimsIdentity(claims, _certificateScheme);
                context.User = new ClaimsPrincipal(identity);
            }
        }

        await _next(context);
    }
}

public static class CertificateAuthenticationMiddlewareExtensions
{
    public static IApplicationBuilder UseCertificateAuthentication(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<CertificateAuthenticationMiddleware>();
    }
}
