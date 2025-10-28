// ===========================================================================
// SADAB Web Application - Entry Point
// ===========================================================================
// This file configures and initializes the Blazor Server application for
// the SADAB management console. It sets up dependency injection, HTTP clients,
// middleware pipeline, and routing for the web interface.
// ===========================================================================

using SADAB.Web.Handlers;
using SADAB.Web.Services;
using SADAB.Web.Settings;

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

// Register certificate services
// These handle certificate storage and API registration for client authentication
builder.Services.AddSingleton<ICertificateStorageService, CertificateStorageService>();
builder.Services.AddSingleton<IApiRegistrationService, ApiRegistrationService>();

// Register HTTP message handler for adding certificate header to requests
builder.Services.AddTransient<CertificateHeaderHandler>();

// Register named HTTP client for SADAB API communication with certificate authentication
// This client automatically adds the X-Client-Certificate-Thumbprint header to all requests
builder.Services.AddHttpClient("SADAB.API", client =>
{
    var apiUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "https://localhost:5001";
    _logger.LogDebug("Configuring SADAB.API HttpClient with BaseAddress: {ApiUrl}", apiUrl);

    client.BaseAddress = new Uri(apiUrl);
})
.AddHttpMessageHandler<CertificateHeaderHandler>();

// Register anonymous HTTP client for initial registration (no certificate required)
builder.Services.AddHttpClient("SADAB.API.Anonymous", client =>
{
    var apiUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "https://localhost:5001";
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

// Register configuration options
// Binds the DashboardSettings section from appsettings.json to the DashboardSettings class
// Uses IOptionsMonitor to support runtime configuration changes
_logger.LogDebug("Registering configuration options...");
builder.Services.Configure<DashboardSettings>(builder.Configuration.GetSection("DashboardSettings"));

var app = builder.Build();


// ===========================================================================
// Web App Certificate Registration
// ===========================================================================
// Ensure the web app has a valid certificate for API authentication
// If no certificate exists or it's expired, register with the API to get a new one

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var certificateStorage = services.GetRequiredService<ICertificateStorageService>();
    var registrationService = services.GetRequiredService<IApiRegistrationService>();

    try
    {
        if (!certificateStorage.HasValidCertificate())
        {
            logger.LogInformation("No valid certificate found. Registering web app with API...");

            var registrationResponse = await registrationService.RegisterWebAppAsync();

            if (registrationResponse != null)
            {
                certificateStorage.StoreCertificate(
                    registrationResponse.Certificate,
                    registrationResponse.PrivateKey);

                logger.LogInformation("Web app registered successfully. AgentId: {AgentId}, Certificate expires: {ExpiryDate}",
                    registrationResponse.AgentId, registrationResponse.ExpiresAt);
            }
            else
            {
                logger.LogError("Failed to register web app with API. Application may not be able to connect to API.");
            }
        }
        else
        {
            logger.LogInformation("Valid certificate found. Thumbprint: {Thumbprint}",
                certificateStorage.GetCertificateThumbprint());
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error during web app certificate initialization");
    }
}

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
