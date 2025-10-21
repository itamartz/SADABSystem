# SADAB.Server/Controllers/
* All the SADAB.Server.Controllers should be under folder v1
* All the Controller should have [Route("api/v1[controller]")]
* Add supported to opentelemetry and send the data to local splunk core - HEC and Token will be in appsetting.json
* Add supported to Scalar
* All Controller should have a Return code explain like 200,201 etc.. will text explain for Swagger
* context.Database.EnsureCreated(); also E=add EnsureMigration()

* CommandExecutorService.cs just try to check if the ExitCode is 0 , but the Exit code Can be 3010 etc... The Exit code should be getting from the CommandExecutionDto as array of Exist code
* InventoryDTOs.cs - Should be add all Win32 also support the option to add a new Win32 Wmi Class
