using Microsoft.AspNetCore.SignalR;

namespace SADAB.API.Hubs;

/// <summary>
/// SignalR Hub for real-time agent updates.
/// Broadcasts agent status changes, heartbeats, and metrics to connected web clients.
/// Used to push updates instead of polling, reducing database load and improving responsiveness.
/// Designed to scale efficiently to 5,000+ agents.
/// </summary>
public class AgentHub : Hub
{
    private readonly ILogger<AgentHub> _logger;

    /// <summary>
    /// Initializes a new instance of the AgentHub.
    /// </summary>
    /// <param name="logger">Logger for tracking connection events and errors</param>
    public AgentHub(ILogger<AgentHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Called when a client connects to the hub.
    /// Logs the connection for monitoring purposes.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected to AgentHub: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub.
    /// Logs the disconnection for monitoring purposes.
    /// </summary>
    /// <param name="exception">Exception that caused the disconnect, if any</param>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogWarning(exception, "Client disconnected from AgentHub with error: {ConnectionId}", Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation("Client disconnected from AgentHub: {ConnectionId}", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
