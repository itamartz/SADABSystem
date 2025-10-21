# SADAB System - Software Deployment and Inventory Management

A comprehensive client-server application for software deployment and inventory management, similar to PDQ Deploy and Inventory.

## Features

### Server (REST API)
- **User Authentication & Authorization**
  - JWT-based authentication
  - User registration and login
  - ASP.NET Core Identity integration
  - Protected API endpoints with Bearer token

- **Agent Management**
  - Certificate-based agent authentication (X.509)
  - Unique 60-day certificates per agent
  - Automatic certificate refresh
  - Agent status monitoring
  - Heartbeat tracking

- **Deployment System**
  - Multiple deployment types:
    - Executable (.exe)
    - MSI Installer (.msi)
    - PowerShell scripts (.ps1)
    - Batch scripts (.bat, .cmd)
    - File copy operations
  - File-based deployment storage
  - Target agent selection
  - Deployment status tracking
  - Real-time result reporting

- **Inventory Collection**
  - Hardware information
  - Installed software
  - Environment variables
  - Running services
  - Historical tracking

- **Remote Command Execution**
  - Execute commands on agents
  - PowerShell support
  - Timeout management
  - Output capture

### Agent (Windows Service)
- **Auto-registration** with server
- **Certificate-based authentication**
- **Automatic certificate refresh** before expiration
- **Deployment execution**
  - Download and execute packages
  - Support for all deployment types
  - Result reporting
- **Inventory collection**
  - WMI-based hardware detection
  - Software inventory from registry
  - Automatic periodic updates
- **Command execution**
  - Remote command support
  - Output capture and reporting
- **Heartbeat monitoring**

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     SADAB Server (API)                       │
│                                                              │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │
│  │   User Auth  │  │  Agent Auth  │  │ Deployments  │     │
│  │   (JWT)      │  │  (X.509)     │  │   Folder     │     │
│  └──────────────┘  └──────────────┘  └──────────────┘     │
│                                                              │
│  ┌────────────────────────────────────────────────────┐    │
│  │          SQLite Database (EF Core)                 │    │
│  │  - Users (Identity)                                │    │
│  │  - Agents                                          │    │
│  │  - Certificates                                    │    │
│  │  - Deployments                                     │    │
│  │  - Inventory                                       │    │
│  │  - Commands                                        │    │
│  └────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
                            ▲
                            │ HTTPS + Certificates
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                  SADAB Agent (Windows Service)               │
│                                                              │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │
│  │  Heartbeat   │  │  Deployment  │  │   Command    │     │
│  │   Monitor    │  │   Executor   │  │   Executor   │     │
│  └──────────────┘  └──────────────┘  └──────────────┘     │
│                                                              │
│  ┌──────────────┐  ┌──────────────┐                        │
│  │  Inventory   │  │ Certificate  │                        │
│  │  Collector   │  │   Manager    │                        │
│  └──────────────┘  └──────────────┘                        │
└─────────────────────────────────────────────────────────────┘
```

## Project Structure

```
SADABSystem/
├── src/
│   ├── SADAB.Server/          # ASP.NET Core Web API
│   │   ├── Controllers/       # API Controllers
│   │   ├── Data/             # EF Core DbContext & Models
│   │   ├── Models/           # Database Entities
│   │   ├── Services/         # Business Logic
│   │   ├── Middleware/       # Custom Middleware
│   │   └── Program.cs
│   │
│   ├── SADAB.Agent/          # Worker Service (Windows Service)
│   │   ├── Services/         # Agent Services
│   │   ├── Configuration/    # Agent Configuration
│   │   ├── Worker.cs         # Main Worker
│   │   └── Program.cs
│   │
│   └── SADAB.Shared/         # Shared Library
│       ├── DTOs/             # Data Transfer Objects
│       ├── Enums/            # Enumerations
│       └── Models/           # Shared Models
│
└── SADABSystem.sln           # Solution File
```

## Getting Started

### Prerequisites

- .NET 8.0 SDK or later
- Windows (for Agent)
- SQLite (included)

### Server Setup

1. **Navigate to the Server project:**
   ```bash
   cd src/SADAB.Server
   ```

2. **Configure settings** in `appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Data Source=sadab.db"
     },
     "JwtSettings": {
       "SecretKey": "YourSecretKeyHere-MustBeAtLeast32Characters",
       "Issuer": "SADAB.Server",
       "Audience": "SADAB.Client",
       "ExpirationHours": "24"
     },
     "DeploymentsPath": "Deployments"
   }
   ```

3. **Run the server:**
   ```bash
   dotnet run
   ```

4. **Access Swagger UI:**
   Open browser to `https://localhost:5001/swagger`

### Agent Setup

1. **Navigate to the Agent project:**
   ```bash
   cd src/SADAB.Agent
   ```

2. **Configure settings** in `appsettings.json`:
   ```json
   {
     "ServerUrl": "https://your-server:5001"
   }
   ```

3. **Run as console application** (for testing):
   ```bash
   dotnet run
   ```

4. **Install as Windows Service:**
   ```bash
   sc create "SADAB Agent" binPath="C:\Path\To\SADAB.Agent.exe"
   sc start "SADAB Agent"
   ```

   Or use PowerShell:
   ```powershell
   New-Service -Name "SADAB Agent" -BinaryPathName "C:\Path\To\SADAB.Agent.exe"
   Start-Service "SADAB Agent"
   ```

## API Usage

### 1. User Registration

```bash
POST /api/auth/register
Content-Type: application/json

{
  "username": "admin",
  "email": "admin@example.com",
  "password": "SecurePassword123!"
}

Response:
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "username": "admin",
  "email": "admin@example.com",
  "expiresAt": "2024-12-01T12:00:00Z"
}
```

### 2. User Login

```bash
POST /api/auth/login
Content-Type: application/json

{
  "username": "admin",
  "password": "SecurePassword123!"
}

Response:
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "username": "admin",
  "email": "admin@example.com",
  "expiresAt": "2024-12-01T12:00:00Z"
}
```

### 3. Get All Agents

```bash
GET /api/agents
Authorization: Bearer {your-jwt-token}

Response:
[
  {
    "id": "guid",
    "machineName": "DESKTOP-001",
    "machineId": "uuid",
    "operatingSystem": "Windows 11",
    "ipAddress": "192.168.1.100",
    "status": 0,
    "lastHeartbeat": "2024-11-01T12:00:00Z",
    "registeredAt": "2024-11-01T10:00:00Z",
    "certificateExpiresAt": "2024-12-31T10:00:00Z"
  }
]
```

### 4. Create Deployment

First, create a folder in the `Deployments` directory:

```bash
# Example: Deployments/MyApp/
#   - setup.exe
#   - config.xml
```

Then create the deployment:

```bash
POST /api/deployments
Authorization: Bearer {your-jwt-token}
Content-Type: application/json

{
  "name": "Install MyApp",
  "description": "Installs MyApp version 1.0",
  "type": 0,
  "packageFolderName": "MyApp",
  "executablePath": "setup.exe",
  "arguments": "/silent /install",
  "targetAgentIds": ["agent-guid-1", "agent-guid-2"],
  "runAsAdmin": true,
  "timeoutMinutes": 30
}
```

Deployment Types:
- `0` = Executable
- `1` = MsiInstaller
- `2` = PowerShell
- `3` = BatchScript
- `4` = FilesCopy

### 5. Execute Remote Command

```bash
POST /api/commands/execute
Authorization: Bearer {your-jwt-token}
Content-Type: application/json

{
  "command": "powershell.exe",
  "arguments": "-Command \"Get-Service | Where-Object {$_.Status -eq 'Running'}\"",
  "targetAgentIds": ["agent-guid-1"],
  "runAsAdmin": false,
  "timeoutMinutes": 5
}
```

### 6. Get Agent Inventory

```bash
GET /api/inventory/agent/{agentId}
Authorization: Bearer {your-jwt-token}

Response:
{
  "agentId": "guid",
  "hardwareInfo": {
    "Processor": "Intel Core i7",
    "TotalMemoryMB": 16384,
    "Disks": [...]
  },
  "installedSoftware": [
    {
      "name": "Microsoft Office",
      "version": "16.0",
      "publisher": "Microsoft",
      "installDate": "2024-01-01T00:00:00Z"
    }
  ],
  "collectedAt": "2024-11-01T12:00:00Z"
}
```

## Deployment Workflow

1. **Prepare Deployment Package**
   - Create a folder in `Deployments/` directory
   - Add your files (executables, scripts, etc.)

2. **Create Deployment via API**
   - Use POST /api/deployments
   - Specify target agents
   - Set execution parameters

3. **Agent Execution**
   - Agent polls for pending deployments
   - Downloads files from server
   - Executes based on deployment type
   - Reports results back to server

4. **Monitor Results**
   - GET /api/deployments/{id}/results
   - View status, exit codes, output

## Security

### Dual Authentication System

1. **User Authentication (JWT)**
   - Users authenticate with username/password
   - Receive JWT token valid for 24 hours
   - All management APIs require valid Bearer token

2. **Agent Authentication (X.509 Certificates)**
   - Each agent receives unique certificate
   - Certificates valid for 60 days
   - Auto-refresh before expiration
   - Certificate thumbprint validation

### Best Practices

- Change default JWT secret key in production
- Use HTTPS in production
- Restrict API access with firewall rules
- Regularly review agent certificates
- Monitor failed authentication attempts

## Database Schema

The system uses SQLite with the following main tables:

- **AspNetUsers** - User accounts (Identity)
- **Agents** - Registered agents
- **AgentCertificates** - Agent certificates
- **Deployments** - Deployment definitions
- **DeploymentTargets** - Agent assignments
- **DeploymentResults** - Execution results
- **InventoryData** - Collected inventory
- **CommandExecutions** - Remote commands

## Configuration

### Server Configuration (appsettings.json)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=sadab.db"
  },
  "JwtSettings": {
    "SecretKey": "YourSecretKey",
    "Issuer": "SADAB.Server",
    "Audience": "SADAB.Client",
    "ExpirationHours": "24"
  },
  "DeploymentsPath": "Deployments"
}
```

### Agent Configuration

The agent stores its configuration in:
`%ProgramData%\SADAB\Agent\config.json`

```json
{
  "ServerUrl": "https://your-server:5001",
  "AgentId": "guid",
  "CertificatePath": "agent.crt",
  "PrivateKeyPath": "agent.key",
  "CertificateThumbprint": "thumbprint",
  "HeartbeatIntervalSeconds": 30,
  "DeploymentCheckIntervalSeconds": 60,
  "CommandCheckIntervalSeconds": 30,
  "InventoryCollectionIntervalMinutes": 60
}
```

## Troubleshooting

### Agent Not Registering

1. Check server URL in `appsettings.json`
2. Verify server is running and accessible
3. Check firewall rules
4. Review agent logs

### Deployments Not Executing

1. Verify deployment folder exists in `Deployments/`
2. Check agent is online (heartbeat)
3. Review deployment results for errors
4. Check file permissions

### Certificate Issues

1. Check certificate expiration date
2. Verify certificate thumbprint
3. Review certificate service logs
4. Manually trigger refresh if needed

## Development

### Building the Solution

```bash
# Build all projects
dotnet build

# Build specific project
dotnet build src/SADAB.Server/SADAB.Server.csproj
```

### Running Tests

```bash
dotnet test
```

### Database Migrations

The database is automatically created on first run using `EnsureCreated()`. For production, consider using migrations:

```bash
cd src/SADAB.Server
dotnet ef migrations add InitialCreate
dotnet ef database update
```

## Contributing

This is a custom deployment system. Contributions, improvements, and bug reports are welcome.

## License

This project is provided as-is for educational and internal use.

## Support

For issues and questions, please review the logs:
- Server logs: Console output or configured logging provider
- Agent logs: Windows Event Viewer or configured logging provider