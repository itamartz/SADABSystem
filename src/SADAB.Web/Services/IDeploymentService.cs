using SADAB.Shared.DTOs;

namespace SADAB.Web.Services;

public interface IDeploymentService
{
    Task<List<DeploymentDto>> GetAllDeploymentsAsync();
    Task<DeploymentDto?> GetDeploymentByIdAsync(Guid id);
    Task<DeploymentDto> CreateDeploymentAsync(CreateDeploymentRequest request);
    Task<bool> StartDeploymentAsync(Guid id);
}
