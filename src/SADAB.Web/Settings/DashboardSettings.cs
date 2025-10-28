namespace SADAB.Web.Settings;

/// <summary>
/// Configuration settings for the dashboard behavior and appearance.
/// Maps to the "DashboardSettings" section in appsettings.json.
/// </summary>
public class DashboardSettings
{
    /// <summary>
    /// Auto-refresh interval in milliseconds for the dashboard data.
    /// Default: 10000ms (10 seconds).
    /// Set to 0 to disable auto-refresh.
    /// </summary>
    public int RefreshIntervalMs { get; set; } = 10000;
}
