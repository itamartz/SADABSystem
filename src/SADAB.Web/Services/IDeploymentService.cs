using SADAB.Shared.DTOs;

namespace SADAB.Web.Services;

/// <summary>
/// Service interface for managing software deployments in the SADAB system.
/// Provides methods to create, retrieve, and control deployment operations across
/// multiple agents. This service communicates with the backend API to orchestrate
/// software distribution and installation on remote machines.
/// </summary>
public interface IDeploymentService
{
    /// <summary>
    /// Retrieves all deployments from the SADAB server.
    /// </summary>
    /// <returns>
    /// A list of <see cref="DeploymentDto"/> objects representing all deployments,
    /// including their status, target counts, and success/failure statistics.
    /// Returns an empty list if no deployments exist or if the API request fails.
    /// </returns>
    /// <remarks>
    /// This method is used by the Dashboard and Deployments pages to display an overview
    /// of all deployment operations. Each deployment includes metadata such as package name,
    /// deployment type (executable, script, file copy), and progress information.
    /// </remarks>
    Task<List<DeploymentDto>> GetAllDeploymentsAsync();

    /// <summary>
    /// Retrieves detailed information for a specific deployment by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier (GUID) of the deployment to retrieve.</param>
    /// <returns>
    /// A <see cref="DeploymentDto"/> object containing comprehensive deployment information,
    /// or null if the deployment is not found or the API request fails.
    /// </returns>
    /// <remarks>
    /// This method provides detailed deployment information including target agent lists,
    /// execution results for each agent, exit codes, output logs, and timing information.
    /// It is typically used when viewing a deployment's detail page or monitoring progress.
    /// </remarks>
    Task<DeploymentDto?> GetDeploymentByIdAsync(Guid id);

    /// <summary>
    /// Creates a new deployment in the SADAB system.
    /// </summary>
    /// <param name="request">
    /// A <see cref="CreateDeploymentRequest"/> object containing all necessary deployment
    /// configuration including package name, target agents, execution parameters, and timeout settings.
    /// </param>
    /// <returns>
    /// A <see cref="DeploymentDto"/> object representing the newly created deployment
    /// with its assigned unique identifier and initial status.
    /// </returns>
    /// <exception cref="Exception">
    /// Thrown if the deployment creation fails due to validation errors, missing packages,
    /// or API communication issues.
    /// </exception>
    /// <remarks>
    /// This method creates a deployment definition in the system but does not immediately
    /// execute it. After creation, call <see cref="StartDeploymentAsync"/> to begin execution.
    /// The deployment package must be uploaded to the server's deployment folder before creation.
    /// </remarks>
    Task<DeploymentDto> CreateDeploymentAsync(CreateDeploymentRequest request);

    /// <summary>
    /// Starts the execution of a previously created deployment.
    /// </summary>
    /// <param name="id">The unique identifier (GUID) of the deployment to start.</param>
    /// <returns>
    /// True if the deployment was successfully queued for execution; false if the
    /// deployment could not be started (e.g., already running, invalid state, or API error).
    /// </returns>
    /// <remarks>
    /// This method initiates the deployment process, which distributes the package to target
    /// agents and executes it according to the configured parameters. The deployment status
    /// will transition from Pending to Running. Progress can be monitored by polling
    /// <see cref="GetDeploymentByIdAsync"/> for updated status and results.
    /// </remarks>
    Task<bool> StartDeploymentAsync(Guid id);
}
