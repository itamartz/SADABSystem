using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using SADAB.Server.Data;
using SADAB.Server.Middleware;
using SADAB.Server.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Clear default logging providers
builder.Logging.ClearProviders();

// Configure OpenTelemetry Logging
builder.Logging.AddOpenTelemetry(x =>
{
    // configure the log exporter to the console
    x.AddConsoleExporter();

    // set service name
    x.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("SADABAPi"));

});


// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger with JWT support
var swaggerSettings = builder.Configuration.GetSection("SwaggerSettings");
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = swaggerSettings["Title"] ?? "SADAB API",
        Version = swaggerSettings["Version"] ?? "v1",
        Description = swaggerSettings["Description"] ?? "SADAB - Software Deployment and Inventory Management System"
    });

    // Include XML documentation
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Configure SQLite Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=sadab.db";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

// Configure Identity
var passwordSettings = builder.Configuration.GetSection("PasswordSettings");
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = passwordSettings.GetValue<bool>("RequireDigit");
    options.Password.RequireLowercase = passwordSettings.GetValue<bool>("RequireLowercase");
    options.Password.RequireUppercase = passwordSettings.GetValue<bool>("RequireUppercase");
    options.Password.RequireNonAlphanumeric = passwordSettings.GetValue<bool>("RequireNonAlphanumeric");
    options.Password.RequiredLength = passwordSettings.GetValue<int>("RequiredLength");
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
});

builder.Services.AddAuthorization();

// Register services
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<ICertificateService, CertificateService>();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        var swaggerTitle = builder.Configuration["SwaggerSettings:Title"] ?? "SADAB API";
        var swaggerVersion = builder.Configuration["SwaggerSettings:Version"] ?? "v1";
        c.SwaggerEndpoint("/swagger/v1/swagger.json", $"{swaggerTitle} {swaggerVersion}");
    });
}

// Ensure database is created and migrations are applied
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();

        //logger.LogDebug("Ensuring database is deleted");
        //context.Database.EnsureDeleted();

        logger.LogDebug("Ensuring database is created");
        context.Database.EnsureCreated();

        logger.LogDebug("Applying database migrations");
        context.Database.Migrate();

        logger.LogInformation("Database initialized successfully");
    }
    catch (Exception ex)
    {
        logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while initializing the database");
    }
}

// Create Deployments folder if it doesn't exist
var deploymentsPath = builder.Configuration["DeploymentSettings:DeploymentsPath"]
    ?? Path.Combine(Directory.GetCurrentDirectory(), "Deployments");

if (!Directory.Exists(deploymentsPath))
{
    Directory.CreateDirectory(deploymentsPath);
    app.Logger.LogInformation("Created Deployments folder at {Path}", deploymentsPath);
}

app.UseHttpsRedirection();

app.UseCors();

// Custom certificate authentication middleware (must run BEFORE UseAuthentication)
app.UseCertificateAuthentication();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

var serviceName = builder.Configuration["ServiceSettings:ServiceName"] ?? "SADAB Server";
app.Logger.LogInformation("{ServiceName} started on {Environment}", serviceName, app.Environment.EnvironmentName);

app.Run();
