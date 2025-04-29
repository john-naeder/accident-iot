using AccidentMonitor.Application;
using AccidentMonitor.Infrastructure;
using AccidentMonitor.Infrastructure.Data;
using AccidentMonitor.Infrastructure.MQTT;
using AccidentMonitor.ServiceDefaults;
using AccidentMonitor.WebApi;


var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();

builder.AddServiceDefaults();

builder.AddKeyVaultIfConfigured();
builder.AddApplicationServices();
builder.AddInfrastructureServices();
builder.AddWebServices();

builder.Services.AddRequestTimeouts();
builder.Services.AddOutputCache();

var app = builder.Build();

await app.InitializeMqttAsync();

if (app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    await app.InitializeDatabaseAsync();
}
else
{
    app.UseHsts();
}

// app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseSwaggerUi(settings =>
{
    settings.Path = "/api";
    settings.DocumentPath = "/api/specification.json";
});

app.MapFallbackToFile("index.html");

app.UseExceptionHandler(options => { });

app.Map("/", () => Results.Redirect("/api"));
app.UseRequestTimeouts();
app.UseOutputCache();
app.MapDefaultEndpoints();
app.MapEndpoints();

await app.RunAsync();
   

public partial class Program { }
