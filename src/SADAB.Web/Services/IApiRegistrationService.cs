using SADAB.Shared.DTOs;

namespace SADAB.Web.Services;

/// <summary>
/// Service for registering the web application as an agent with the API server
/// </summary>
public interface IApiRegistrationService
{
    /// <summary>
    /// Registers the web application with the API and receives a certificate
    /// </summary>
    /// <returns>Registration response containing certificate and private key</returns>
    Task<AgentRegistrationResponse?> RegisterWebAppAsync();
}
