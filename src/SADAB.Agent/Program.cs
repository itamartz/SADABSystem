using SADAB.Agent;
using SADAB.Agent.Configuration;
using SADAB.Agent.Services;
using System.Text.Json;

var builder = Host.CreateApplicationBuilder(args);

// Load agent configuration
var configPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
    "SADAB", "Agent", "config.json");

var agentConfig = new AgentConfiguration();

if (File.Exists(configPath))
{
    try
    {
        var json = await File.ReadAllTextAsync(configPath);
        var loadedConfig = JsonSerializer.Deserialize<AgentConfiguration>(json);
        if (loadedConfig != null)
        {
            agentConfig = loadedConfig;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error loading configuration: {ex.Message}");
    }
}

// Override with appsettings values if present
var serverUrl = builder.Configuration["ServerUrl"];
if (!string.IsNullOrEmpty(serverUrl))
{
    agentConfig.ServerUrl = serverUrl;
}

// Register configuration as singleton
builder.Services.AddSingleton(agentConfig);

// Register HTTP client and API client service
builder.Services.AddHttpClient<IApiClientService, ApiClientService>();

// Register services
builder.Services.AddSingleton<IDeploymentExecutorService, DeploymentExecutorService>();
builder.Services.AddSingleton<ICommandExecutorService, CommandExecutorService>();

if (OperatingSystem.IsWindows())
{
    builder.Services.AddSingleton<IInventoryCollectorService, WindowsInventoryCollectorService>();
}
else
{
    builder.Services.AddSingleton<IInventoryCollectorService, GenericInventoryCollectorService>();
}

// Register worker
builder.Services.AddHostedService<Worker>();

// Configure Windows Service
if (OperatingSystem.IsWindows())
{
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "SADAB Agent";
    });
}

var host = builder.Build();

host.Run();
