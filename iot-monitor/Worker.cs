using System.Text.Json;
using Microsoft.Extensions.Options;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using iot_monitor.Application.Interfaces;
using iot_monitor.Application.Models;
using iot_monitor.Infrastructure.InfluxDb;
using iot_monitor.Infrastructure.MQTT;

namespace IotMonitorService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IMqttService _mqttService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IDeviceAnalysisService _deviceAnalysisService;
        private readonly InfluxDbSettings _influxDbSettings;

        public Worker(
            ILogger<Worker> logger,
            IMqttService mqttService,
            IServiceProvider serviceProvider,
            IDeviceAnalysisService deviceAnalysisService,
            IOptions<InfluxDbSettings> influxDbSettings)
        {
            _logger = logger;
            _mqttService = mqttService;
            _serviceProvider = serviceProvider;
            _deviceAnalysisService = deviceAnalysisService;
            _influxDbSettings = influxDbSettings.Value;
        }        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Worker starting.");

            if (_mqttService == null)
            {
                _logger.LogError("IMqttService was not resolved correctly.");
                throw new InvalidOperationException("IMqttService was not resolved correctly.");
            }
            _mqttService.OnMessageReceivedAsync += HandleMqttMessageAsync;

            var startResult = await _mqttService.StartAsync(cancellationToken);
            if (!startResult.IsSuccess)
            {
                _logger.LogError("Failed to start MQTT Service: {Message}. Worker will not process messages.", startResult.Message);
                 throw new Exception($"Failed to start MQTT Service: {startResult.Message}");
            }
            else
            {
                _logger.LogInformation("MQTT Service started successfully by Worker.");
            }

            await base.StartAsync(cancellationToken);
        }        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
        }private async Task HandleMqttMessageAsync(string topic, string payload)
        {
            _logger.LogInformation("Worker received message via event on topic '{topic}': {payload}", topic, payload);
            
            try
            {
                var deviceId = TopicParser.ExtractId("iot/rsu/{id}", topic);
                if (deviceId != null)
                {
                    // First, store the raw data in InfluxDB
                    using var scope = _serviceProvider.CreateScope();
                    var influxDbClient = scope.ServiceProvider.GetRequiredService<InfluxDBClient>();

                    if (influxDbClient == null)
                    {
                        _logger.LogError("InfluxDBClient could not be resolved within the message handler scope.");
                        return;
                    }

                    try
                    {
                        using var jsonDoc = JsonDocument.Parse(payload);
                        var root = jsonDoc.RootElement;

                        var point = PointData.Measurement("device_status")
                                            .Tag("deviceId", deviceId)
                                            .Timestamp(DateTime.UtcNow, WritePrecision.Ns);

                        if (root.TryGetProperty("isOnline", out var onlineElement) && (onlineElement.ValueKind == JsonValueKind.True || onlineElement.ValueKind == JsonValueKind.False))
                            point = point.Field("isOnline", onlineElement.GetBoolean());
                        if (root.TryGetProperty("batteryLevel", out var batteryElement) && (batteryElement.ValueKind == JsonValueKind.Number))
                            point = point.Field("batteryLevel", batteryElement.GetDouble());
                        if (root.TryGetProperty("signalStrength", out var signalElement) && (signalElement.ValueKind == JsonValueKind.Number))
                            point = point.Field("signalStrength", signalElement.GetDouble());
                        if (root.TryGetProperty("location", out var locationElement) && (locationElement.ValueKind == JsonValueKind.Object))
                        {
                            if (locationElement.TryGetProperty("latitude", out var latElement) && latElement.ValueKind == JsonValueKind.Number)
                                point = point.Field("latitude", latElement.GetDouble());
                            if (locationElement.TryGetProperty("longitude", out var lonElement) && lonElement.ValueKind == JsonValueKind.Number)
                                point = point.Field("longitude", lonElement.GetDouble());
                        }
                        if (root.TryGetProperty("address", out var addressElement) && (addressElement.ValueKind == JsonValueKind.String))
                            point = point.Field("address", addressElement.GetString());

                        if (point.HasFields())
                        {
                            await influxDbClient.GetWriteApiAsync().WritePointAsync(point, _influxDbSettings.Bucket, _influxDbSettings.Org);
                            _logger.LogInformation("Wrote status for device '{deviceId}' to InfluxDB.", deviceId);
                        }
                        else
                        {
                            _logger.LogWarning("No valid fields found for device '{deviceId}'. Skipping InfluxDB write.", deviceId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error writing raw device data to InfluxDB for device '{deviceId}'", deviceId);
                    }

                    // Then process and analyze the device data
                    try
                    {
                        var deviceData = JsonSerializer.Deserialize<Dictionary<string, object>>(payload, 
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        
                        if (deviceData != null)
                        {
                            var analysisResult = await _deviceAnalysisService.AnalyzeDeviceDataAsync(deviceId, deviceData);
                            await _deviceAnalysisService.SaveAnalysisHistoryAsync(analysisResult);
                            
                            _logger.LogInformation("Analyzed data for device '{deviceId}', status: {Status}", 
                                deviceId, analysisResult.Status);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error analyzing device data for device '{deviceId}'", deviceId);
                    }
                }
                else
                {
                    _logger.LogWarning("Received message on unhandled topic format: {topic}", topic);
                }
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "Failed to parse JSON payload from topic '{topic}'. Payload: {payload}", topic, payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message from topic '{topic}'", topic);
            }
        }        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Worker stopping.");

            if (_mqttService != null)
            {
                _mqttService.OnMessageReceivedAsync -= HandleMqttMessageAsync;
            }

            await base.StopAsync(cancellationToken);
        }
    }
}