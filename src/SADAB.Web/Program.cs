// ===========================================================================
// SADAB Web Application - Entry Point
// ===========================================================================
// This file configures and initializes the Blazor Server application for
// the SADAB management console. It sets up dependency injection, HTTP clients,
// middleware pipeline, and routing for the web interface.
// ===========================================================================

using SADAB.Web.Services;

var builder = WebApplication.CreateBuilder(args);

ILogger _logger = LoggerFactory.Create(logging =>
{
    logging.AddConsole();
}).CreateLogger<Program>();

_logger.LogInformation("Starting Web Server...");

// ===========================================================================
// Service Registration
// ===========================================================================

// Add Razor Pages support
// Required for hosting the _Host.cshtml page which bootstraps the Blazor app
builder.Services.AddRazorPages();

// Add Blazor Server services
// Enables server-side Blazor with SignalR for real-time UI updates
builder.Services.AddServerSideBlazor();

// Register named HTTP client for SADAB API communication
// This client is configured with the base URL from appsettings.json and is
// used by all service classes to communicate with the backend API
builder.Services.AddHttpClient("SADAB.API", client =>
{
    var apiUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "https://localhost:5001";
    _logger.LogDebug("Configuring SADAB.API HttpClient with BaseAddress: {ApiUrl}", apiUrl);

    client.BaseAddress = new Uri(apiUrl);
});

// Register application services with scoped lifetime
// Scoped services are created once per Blazor circuit (user session)
// This ensures each user has their own service instances for the duration of their session

_logger.LogDebug("Registering application services...");
_logger.LogDebug("Registered IAgentService with implementation AgentService");
builder.Services.AddScoped<IAgentService, AgentService>();

_logger.LogDebug("Registered IDeploymentService with implementation DeploymentService");
builder.Services.AddScoped<IDeploymentService, DeploymentService>();

_logger.LogDebug("Registered ICommandService with implementation CommandService");
builder.Services.AddScoped<ICommandService, CommandService>();

var app = builder.Build();


// ===========================================================================
// HTTP Request Pipeline Configuration
// ===========================================================================

// Configure error handling and security for production
if (!app.Environment.IsDevelopment())
{
    // Use error handler for unhandled exceptions
    app.UseExceptionHandler("/Error");

    // Enable HTTP Strict Transport Security (HSTS)
    // Forces browsers to use HTTPS for all future requests
    app.UseHsts();
}

// Redirect HTTP requests to HTTPS
app.UseHttpsRedirection();

// Enable serving static files (CSS, JavaScript, images)
app.UseStaticFiles();

// Enable endpoint routing
app.UseRouting();

// Map SignalR hub for Blazor Server
// This establishes the WebSocket connection for real-time communication
app.MapBlazorHub();

// Map fallback route to _Host.cshtml
// All routes not matched by other endpoints will be handled by Blazor
app.MapFallbackToPage("/_Host");

// Start the application
app.Run();
