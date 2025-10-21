using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using SADAB.Server.Services;

namespace SADAB.Server.Middleware;

public class CertificateAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CertificateAuthenticationMiddleware> _logger;

    public CertificateAuthenticationMiddleware(
        RequestDelegate next,
        ILogger<CertificateAuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ICertificateService certificateService)
    {
        // Only apply to agent endpoints
        if (!context.Request.Path.StartsWithSegments("/api/agents"))
        {
            await _next(context);
            return;
        }

        // Skip authentication for registration endpoint
        if (context.Request.Path.StartsWithSegments("/api/agents/register"))
        {
            await _next(context);
            return;
        }

        var clientCertificate = context.Connection.ClientCertificate;

        if (clientCertificate == null)
        {
            // Try to get certificate from header (for development/testing)
            var certThumbprint = context.Request.Headers["X-Client-Certificate-Thumbprint"].FirstOrDefault();

            if (string.IsNullOrEmpty(certThumbprint))
            {
                _logger.LogWarning("No client certificate provided for agent endpoint");
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Client certificate required");
                return;
            }

            // Validate thumbprint from header
            var isValid = await certificateService.ValidateCertificateAsync(certThumbprint);
            if (!isValid)
            {
                _logger.LogWarning("Invalid certificate thumbprint: {Thumbprint}", certThumbprint);
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Invalid certificate");
                return;
            }

            var agentCert = await certificateService.GetCertificateByThumbprintAsync(certThumbprint);
            if (agentCert != null)
            {
                // Add agent claims
                var claims = new[]
                {
                    new Claim("AgentId", agentCert.AgentId.ToString()),
                    new Claim("CertificateThumbprint", certThumbprint),
                    new Claim(ClaimTypes.Role, "Agent")
                };

                var identity = new ClaimsIdentity(claims, "Certificate");
                context.User = new ClaimsPrincipal(identity);
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
                await context.Response.WriteAsync("Invalid certificate");
                return;
            }

            var agentCert = await certificateService.GetCertificateByThumbprintAsync(thumbprint);
            if (agentCert != null)
            {
                // Add agent claims
                var claims = new[]
                {
                    new Claim("AgentId", agentCert.AgentId.ToString()),
                    new Claim("CertificateThumbprint", thumbprint),
                    new Claim(ClaimTypes.Role, "Agent")
                };

                var identity = new ClaimsIdentity(claims, "Certificate");
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
