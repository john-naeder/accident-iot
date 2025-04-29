using iot_monitor.Infrastructure.MQTT;
using iot_monitor.Infrastructure.InfluxDb;
using iot_monitor.Infrastructure.Analysis;
using iot_monitor.Infrastructure.Notifications;
using IotMonitorService;
using InfluxDB.Client;
using Microsoft.Extensions.Options;
using iot_monitor.Application.Interfaces;
using System.Net.Http;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);


builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();

builder.Services.Configure<MqttSettings>(builder.Configuration.GetSection("MqttConnectionConfig"));
builder.Services.Configure<InfluxDbSettings>(builder.Configuration.GetSection("InfluxDbSettings"));
builder.Services.Configure<DeviceAnalysisSettings>(builder.Configuration.GetSection("DeviceAnalysisSettings"));
builder.Services.Configure<NotificationSettings>(builder.Configuration.GetSection("NotificationSettings"));


builder.Services.AddSingleton<InfluxDBClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<InfluxDbSettings>>().Value;
    if (string.IsNullOrEmpty(options.Url) || string.IsNullOrEmpty(options.Token))
    {
        var logger = sp.GetRequiredService<ILogger<Program>>();
        logger.LogError("InfluxDB URL or Token is not configured properly.");
        throw new InvalidOperationException("InfluxDB URL or Token is not configured.");
    }
    return new InfluxDBClient(options.Url, options.Token);
    // var influxDBClient = new InfluxDBClient(options.Url, options.Token);
    //var health = await influxDBClient.PingAsync();
    //if (!health)
    //{
    //    var logger = sp.GetRequiredService<ILogger<Program>>();
    //    logger.LogError(message: "Failed to connect to InfluxDB: ");
    //    throw new InvalidOperationException("Failed to connect to InfluxDB.");
    //}
    //else
    //{
    //    var logger = sp.GetRequiredService<ILogger<Program>>();
    //    logger.LogInformation("Connected to InfluxDB successfully.");
    //}    return new InfluxDBClient(options.Url, options.Token);
});

// Add Http Client Factory for notification service
builder.Services.AddHttpClient("NotificationClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Register services
builder.Services.AddSingleton<IMqttService, MqttClientService>();
builder.Services.AddSingleton<IDeviceAnalysisService, DeviceAnalysisService>();
builder.Services.AddSingleton<INotificationService, NotificationService>();

builder.Services.AddHostedService<Worker>();

IHost host = builder.Build();
host.Run();