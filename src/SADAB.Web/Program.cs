using SADAB.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Register HTTP client for API calls
builder.Services.AddHttpClient("SADAB.API", client =>
{
    var apiUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "https://localhost:5001";
    client.BaseAddress = new Uri(apiUrl);
});

// Register application services
builder.Services.AddScoped<IAgentService, AgentService>();
builder.Services.AddScoped<IDeploymentService, DeploymentService>();
builder.Services.AddScoped<ICommandService, CommandService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
