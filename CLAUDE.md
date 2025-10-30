# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

SADAB (Software Deployment and Inventory Management System) is a comprehensive client-server application similar to PDQ Deploy and Inventory. The system consists of four main components:
- **SADAB.API**: ASP.NET Core REST API server
- **SADAB.Agent**: Windows Service that runs on managed machines
- **SADAB.Web**: Blazor Server management console
- **SADAB.Shared**: Common DTOs, enums, and models

## Build and Run Commands

### Build the entire solution
```bash
dotnet build SADABSystem.sln
```

### Build individual projects
```bash
dotnet build src/SADAB.API/SADAB.API.csproj
dotnet build src/SADAB.Agent/SADAB.Agent.csproj
dotnet build src/SADAB.Web/SADAB.Web.csproj
dotnet build src/SADAB.Shared/SADAB.Shared.csproj
```

### Run projects
```bash
# Run API server (from src/SADAB.API)
dotnet run --project src/SADAB.API/SADAB.API.csproj

# Run Web console (from src/SADAB.Web)
dotnet run --project src/SADAB.Web/SADAB.Web.csproj

# Run Agent (from src/SADAB.Agent)
dotnet run --project src/SADAB.Agent/SADAB.Agent.csproj
```

### Database migrations
```bash
cd src/SADAB.API
dotnet ef migrations add <MigrationName>
dotnet ef database update
```

Note: The project currently uses `EnsureCreated()` for database initialization. Migrations are available but not actively used.

## Architecture Overview

### Dual Authentication System
The system implements two separate authentication mechanisms:
1. **User Authentication (JWT)**: For human users accessing the API/Web interface
   - Configured in `JwtSettings` section of appsettings.json
   - Token service: `SADAB.API/Services/TokenService.cs`
   - Controllers: `SADAB.API/Controllers/AuthController.cs`

2. **Agent Authentication (X.509 Certificates)**: For machine-to-machine communication
   - Each agent receives a unique 60-day certificate
   - Certificate middleware: `SADAB.API/Middleware/CertificateAuthenticationMiddleware.cs`
   - Certificate service: `SADAB.API/Services/CertificateService.cs`
   - Certificates passed via `X-Client-Certificate-Thumbprint` header (configurable)

### Key Middleware Order (SADAB.API/Program.cs)
The middleware pipeline order is critical for SignalR to work:
```
1. UseHttpsRedirection()
2. UseRouting()                    // MUST come before CORS for SignalR
3. UseCors()                       // MUST come after UseRouting for SignalR
4. UseCertificateAuthentication()  // Custom: MUST run before UseAuthentication
5. UseAuthentication()
6. UseAuthorization()
7. MapControllers()
8. MapHub<AgentHub>("/hubs/agents").AllowAnonymous()  // SignalR endpoint
```

**Critical**: `UseRouting()` must be called explicitly before `UseCors()` for SignalR negotiation to work properly.

### Agent Background Worker Pattern
The Agent (`SADAB.Agent/Worker.cs`) runs multiple concurrent background loops:
- **HeartbeatLoop**: Sends periodic status updates with system metrics
- **DeploymentCheckLoop**: Polls for pending deployments
- **CommandCheckLoop**: Polls for pending commands (currently disabled)
- **InventoryCollectionLoop**: Collects and submits inventory data (currently disabled)
- **CertificateRefreshLoop**: Auto-refreshes certificates before expiration (currently disabled)
- **ConfigurationReloadLoop**: Reloads configuration from disk every 2 minutes

All intervals are configurable via `appsettings.json` and stored in `AgentConfiguration`.

### Agent System Information Collection
The agent collects and sends system metrics with each heartbeat in the `SystemInfo` dictionary:

**Collected Metrics**:
- `MachineName`: Computer name
- `UserName`: Current user running the agent
- `OSVersion`: Formatted as "Windows 11 Pro (22631.4602)" - OS caption with build.UBR
- `AgentVersion`: Version from configuration
- `CpuUsagePercent`: System-wide CPU usage (0-100%) using PerformanceCounter
- `MemoryUsagePercent`: Physical memory usage (0-100%) calculated as (used / total) × 100

**Implementation Details**:
- OS version uses WMI `Win32_OperatingSystem.Caption` and registry `UBR` value
- "Microsoft " prefix automatically stripped from OS name
- CPU/Memory metrics use Windows PerformanceCounters (returns -1 on non-Windows)
- All metrics stored in `Agent.Metadata` JSON field in database
- API extracts CPU/Memory from metadata and includes in AgentDto responses

### Configuration Pattern
All projects follow a strict configuration pattern:
- **All user-facing strings** must come from the `Messages` section of appsettings.json
- **All settings** must come from appropriate sections (JwtSettings, CertificateSettings, DeploymentSettings, etc.)
- Services must use `IConfiguration` dependency injection
- No hardcoded strings or magic numbers

Example:
```csharp
var message = _configuration["Messages:ErrorOccurred"] ?? "An error occurred";
var timeout = _configuration.GetValue<int>("DeploymentSettings:DefaultTimeoutMinutes");
```

### Blazor Web Application Pattern
SADAB.Web is a Blazor Server app with:
- **Certificate-based authentication** with the API
- **Named HttpClient instances**: `SADAB.API` (with certificate) and `SADAB.API.Anonymous` (without)
- **CertificateHeaderHandler**: Automatically adds certificate thumbprint to all authenticated requests
- **Auto-registration**: On startup, checks for valid certificate and registers with API if needed
- **Scoped services**: All application services (AgentService, DeploymentService, CommandService) use scoped lifetime per Blazor circuit

### SignalR Real-Time Updates Architecture
The system uses SignalR for real-time agent updates, designed to scale to 5,000+ agents efficiently:

**API Side** (`SADAB.API/Hubs/AgentHub.cs`):
- Hub endpoint at `/hubs/agents` (allow anonymous access for web dashboard)
- Broadcasts "AgentUpdated" messages when agents send heartbeats
- Only sends the updated agent data, not all 5K agents

**Web Side** (Dashboard and Agents pages):
- SignalR connection with automatic reconnect (exponential backoff)
- Receives individual agent updates and updates local state
- Fallback timer polling when SignalR disconnected
- Connection state tracking to determine when fallback needed

**Fallback Strategy**:
- Dashboard: 10-second polling (configurable via `DashboardSettings:RefreshIntervalMs`)
- Agents page: 60-second polling (configurable via `DashboardSettings:FallbackRefreshIntervalMs`)
- Timer only runs when `_isSignalRConnected = false`
- Automatic switch between SignalR and polling based on connection state

**Performance at Scale**:
- With SignalR: ~10 broadcasts/min (only when agents heartbeat), < 1 second latency
- Without SignalR: 6 full API calls/min per client, 10-60 second latency
- Bandwidth: 1 agent update vs 5K agent list per refresh

**Key Implementation Details**:
- `BroadcastAgentUpdateAsync()` in `AgentsController.cs` extracts data from metadata JSON
- Web pages use `HandleAgentUpdateAsync()` to update individual agents in list
- `InitializeSignalRAsync()` configures logging level to Debug for troubleshooting

### Data Storage
- **Database**: SQLite with Entity Framework Core
- **DbContext**: `SADAB.API/Data/ApplicationDbContext.cs`
- **Models**: `SADAB.API/Models/` (Agent, Deployment, Certificate, Inventory, etc.)
- **DTOs**: `SADAB.Shared/DTOs/` (used for API communication)
- **Deployment files**: Stored in file system under `Deployments/` folder (configurable)

### File-based Deployment System
Deployments work via folder structure:
1. Create folder in `Deployments/<PackageName>/`
2. Add files (executables, scripts, etc.)
3. Create deployment via API with `packageFolderName` pointing to folder
4. Agent downloads files to temp directory and executes based on `DeploymentType`

Supported types (see `SADAB.Shared/Enums/DeploymentType.cs`):
- Executable (.exe)
- MsiInstaller (.msi)
- PowerShell (.ps1)
- BatchScript (.bat, .cmd)
- FilesCopy (directory copy)

## Code Conventions

### Naming
- Classes, methods, properties: PascalCase
- Private fields: _camelCase with underscore prefix
- Local variables: camelCase
- Constants: PascalCase or UPPER_CASE

### Required Patterns

#### 1. ToString() Extension Method Pattern
All DTOs and models must implement ToString() for debugging using the `ToKeyValueString()` extension method:

**Location**: `SADAB.Shared/Extensions/ObjectExtensions.cs`

**Usage**:
```csharp
using SADAB.Shared.Extensions;

public class MyDto
{
    public string Name { get; set; }
    public int Age { get; set; }
    public string? Password { get; set; }

    /// <summary>
    /// Returns a string representation with all properties in Key=Value format using reflection.
    /// </summary>
    public override string ToString() => this.ToKeyValueString();
}
```

**Automatic Handling**:
- **Sensitive fields**: Properties containing "Password", "PrivateKey" are masked as "***"
- **Tokens**: Truncated to first 20 characters
- **Certificates**: Truncated to first 50 characters
- **Long outputs**: Output/ErrorOutput/ErrorMessage truncated to 50 characters
- **Dictionaries**: Expanded to show all key-value pairs `[Key1=Value1, Key2=Value2]`
- **Lists**: Show item count or expanded based on type
- **DateTime**: Formatted as "yyyy-MM-dd HH:mm:ss"
- **Null values**: Display as "null"

**Benefits**:
- Future-proof: New properties automatically appear in ToString() output
- Consistent: All DTOs follow the same pattern
- Secure: Sensitive data automatically masked
- Maintainable: Logic lives in one place

**Example Output**:
```
Status=Online, IpAddress=192.168.1.100, SystemInfo=[MachineName=SERVER01, OSVersion=Windows 10, AgentVersion=1.0.1]
```

2. **XML documentation**: All public classes and methods should have XML comments

3. **Logging with ILogger - NEVER use Console.WriteLine**:
   All logging must use structured logging via `ILogger<T>` dependency injection.

   **Logging Levels**:
   - `LogDebug`: Variable values, detailed flow, SignalR events, resource disposal
   - `LogInformation`: Important milestones, connections established, operations completed
   - `LogWarning`: Recoverable issues, connection losses, authorization failures
   - `LogError`: Exceptions, operation failures requiring attention

   **Example logging pattern**:
   ```csharp
   _logger.LogDebug("Processing request with parameter: {Parameter}", parameter);
   _logger.LogInformation("SignalR connected successfully to {HubUrl}", hubUrl);
   _logger.LogWarning(error, "Connection lost, attempting to reconnect");
   _logger.LogError(ex, "Failed to load data from API");
   ```

   **Structured Parameters**:
   Use curly braces for queryable parameters: `{AgentId}`, `{MachineName}`, `{HubUrl}`

   **Exception Logging**:
   Always pass exception as first parameter: `_logger.LogError(ex, "Error message")`

   **DO NOT**:
   - ❌ Never use `Console.WriteLine()` - not production-ready
   - ❌ Never use string concatenation in log messages - use structured parameters
   - ❌ Never log without proper context parameters

4. **Dependency Injection**: Use constructor injection for all services
5. **Configuration injection**: Always inject `IConfiguration` rather than hardcode values

### DTO ToString() Implementation Checklist
When creating a new DTO:
1. Add `using SADAB.Shared.Extensions;` at the top
2. Override ToString() with: `public override string ToString() => this.ToKeyValueString();`
3. Add XML documentation comment describing the output
4. No additional logic needed - the extension handles everything

### Project Structure Notes
- **Controllers** are thin and delegate to services
- **Services** contain business logic
- **Middleware** handles cross-cutting concerns (authentication, logging)
- **DTOs** in Shared project for API contracts
- **Models** in API project for database entities

## Development Workflow

### Branching Strategy
**IMPORTANT**: Always create a git branch BEFORE starting any code changes in SADAB.

- **Create branch first**: Before making any code modifications, create a new feature branch
- Ask for descriptive-name and use it in the branch naming
- Feature branch naming: `claude/<descriptive-name>-<dateTime>`
- Make all changes in the feature branch
- Never push directly to main
- Create PR after feature completion

**Example workflow**:
```bash
# 1. Create new branch before starting work
git checkout -b claude/feature-name-20251030

# 2. Make your changes in the branch
# ... edit files ...

# 3. Commit changes to the branch
git add .
git commit -m "Your commit message"

# 4. Push branch and create PR
git push -u origin claude/feature-name-20251030
```

### Commit Pattern
- Frequent commits with descriptive messages
- Always include "🤖 Generated with [Claude Code](https://claude.com/claude-code)" footer
- Include "Co-Authored-By: Claude <noreply@anthropic.com>"
- Use multi-line commit messages with bullet points for changes

### Testing
- Run build before committing: `dotnet build`
- Test locally when possible
- No automated test suite currently exists

## Important Implementation Details

### Agent Configuration Persistence
Agent stores configuration at: `%ProgramData%\SADAB\Agent\config.json` (Windows)
Or working directory on other platforms. The agent reloads this file every 2 minutes.

### Certificate Header Name
Configurable via `ServiceSettings:CertificateHeaderName` (default: `X-Client-Certificate-Thumbprint`)

### OpenTelemetry Logging
API uses OpenTelemetry with Console exporter for structured logging.
Configured in `SADAB.API/Program.cs` (lines 15-27)

### Deployment Folder Auto-creation
API automatically creates the Deployments folder on startup if it doesn't exist.

### Swagger/OpenAPI
Available in Development mode at `/swagger`
Configured with JWT Bearer token support

## Configuration Sections Reference

### appsettings.json sections (SADAB.API):
- `ConnectionStrings`: Database connection
- `JwtSettings`: JWT token configuration
- `CertificateSettings`: X.509 certificate parameters
- `DeploymentSettings`: Deployment folder and timeout settings
- `PasswordSettings`: Password requirements
- `SwaggerSettings`: Swagger UI configuration
- `Messages`: All user-facing error/info messages
- `ServiceSettings`: Service name and header names
- `SecuritySettings`: Security features (local bypass, etc.)
- `CorsSettings`: CORS origins for SignalR (comma-separated, e.g., "https://localhost:5002,http://localhost:5002")

### appsettings.json sections (SADAB.Agent):
- `ServerUrl`: API server URL
- `AgentSettings`: File names, intervals, logging flags
- `Messages`: User-facing messages for agent

### appsettings.json sections (SADAB.Web):
- `ApiSettings:BaseUrl`: API server URL (used for SignalR hub connection)
- `DashboardSettings:RefreshIntervalMs`: Dashboard fallback polling interval (default 10000ms)
- `DashboardSettings:FallbackRefreshIntervalMs`: Agents page fallback polling interval (default 60000ms)

## Common Tasks

### Add a new API endpoint
1. Create/update controller in `SADAB.API/Controllers/`
2. Add DTOs to `SADAB.Shared/DTOs/`
3. Implement business logic in services
4. Add XML documentation comments
5. Test with Swagger

### Add a new deployment type
1. Add enum value to `SADAB.Shared/Enums/DeploymentType.cs`
2. Update `DeploymentExecutorService.cs` to handle new type
3. Update API validation if needed

### Modify authentication
- User auth: Update `SADAB.API/Services/TokenService.cs`
- Agent auth: Update `SADAB.API/Middleware/CertificateAuthenticationMiddleware.cs`
- Certificate generation: Update `SADAB.API/Services/CertificateService.cs`

### Add configuration setting
1. Add to appropriate section in `appsettings.json`
2. Access via `IConfiguration` injection
3. Use `GetValue<T>()` for typed values or indexer for strings
4. Provide default values with null-coalescing operator

## Known Patterns to Follow

1. Always read files before editing
2. Use multi-step TODO lists for complex tasks
3. Prefer editing existing files over creating new ones
4. Use `ToString()` overrides on all DTOs/models using `ToKeyValueString()` extension method
5. Configuration over hardcoding (no magic strings or numbers)
6. **NEVER use Console.WriteLine** - always use ILogger with structured logging
7. Comprehensive error handling with proper logging levels
8. Separate concerns: Controllers → Services → Data layer
9. Follow existing naming conventions and project structure
10. Document object in logger.LogDebug calls for easier tracing
11. Use the `ToKeyValueString()` extension method for all new DTOs (never write manual ToString() logic)
12. When adding real-time features, use SignalR with fallback timer pattern
13. Store dynamic/temporary data in JSON metadata fields rather than adding database columns
14. Inject ILogger<T> for all logging needs in components and services 
