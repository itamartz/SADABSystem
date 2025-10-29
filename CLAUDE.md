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
The middleware pipeline order is critical:
```
1. UseHttpsRedirection()
2. UseCors()
#3. UseLocalConnectionBypass()  // Custom: bypasses auth for localhost connections if enabled
4. UseCertificateAuthentication()  // Custom: MUST run before UseAuthentication
5. UseAuthentication()
6. UseAuthorization()
```

### Agent Background Worker Pattern
The Agent (`SADAB.Agent/Worker.cs`) runs multiple concurrent background loops:
- **HeartbeatLoop**: Sends periodic status updates
- **DeploymentCheckLoop**: Polls for pending deployments
- **CommandCheckLoop**: Polls for pending commands (currently disabled)
- **InventoryCollectionLoop**: Collects and submits inventory data (currently disabled)
- **CertificateRefreshLoop**: Auto-refreshes certificates before expiration (currently disabled)
- **ConfigurationReloadLoop**: Reloads configuration from disk every 2 minutes

All intervals are configurable via `appsettings.json` and stored in `AgentConfiguration`.

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
1. **ToString() overrides**: All DTOs and models must implement ToString() for debugging
2. **XML documentation**: All public classes and methods should have XML comments
3. **Logging levels**:
   - `LogDebug`: For variable values and detailed flow
   - `LogInformation`: For user-facing information
   - `LogWarning`: For authorization failures and sensitive data masking
   - `LogError`: For exceptions and failures

Example logging pattern:
```csharp
_logger.LogDebug("Processing request with parameter: {Parameter}", parameter);
_logger.LogInformation("Operation completed successfully");
_logger.LogWarning("Unauthorized access attempt from {IpAddress}", ipAddress);
```

4. **Dependency Injection**: Use constructor injection for all services
5. **Configuration injection**: Always inject `IConfiguration` rather than hardcode values

### Project Structure Notes
- **Controllers** are thin and delegate to services
- **Services** contain business logic
- **Middleware** handles cross-cutting concerns (authentication, logging)
- **DTOs** in Shared project for API contracts
- **Models** in API project for database entities

## Development Workflow

### Branching Strategy
- Feature branches: `claude/<descriptive-name>-<dateTime>`
- Never push directly to main
- Create PR after feature completion

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

### appsettings.json sections (SADAB.Agent):
- `ServerUrl`: API server URL
- `AgentSettings`: File names, intervals, logging flags
- `Messages`: User-facing messages for agent

### appsettings.json sections (SADAB.Web):
- `ApiSettings:BaseUrl`: API server URL
- `DashboardSettings`: Auto-refresh and polling intervals

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
4. Use `ToString()` overrides on all DTOs/models for debugging
5. Configuration over hardcoding (no magic strings or numbers)
6. Comprehensive error handling with proper logging levels
7. Separate concerns: Controllers → Services → Data layer
8. Follow existing naming conventions and project structure
9. Document object in logger.LogDebug calls for easier tracing 
