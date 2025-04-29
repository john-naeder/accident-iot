using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using iot_monitor.Application.Interfaces;
using iot_monitor.Infrastructure.InfluxDb;
using iot_monitor.Application.Models;
using iot_monitor.Application.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace iot_monitor.Infrastructure.Analysis
{    
    public class DeviceAnalysisService : IDeviceAnalysisService
    {
        private readonly ILogger<DeviceAnalysisService> _logger;
        private readonly InfluxDBClient _influxDbClient;
        private readonly InfluxDbSettings _influxDbSettings;
        private readonly DeviceAnalysisSettings _analysisSettings;
        private readonly INotificationService _notificationService;

        public DeviceAnalysisService(
            ILogger<DeviceAnalysisService> logger,
            InfluxDBClient influxDbClient,
            IOptions<InfluxDbSettings> influxDbSettings,
            IOptions<DeviceAnalysisSettings> analysisSettings,
            INotificationService notificationService)
        {
            _logger = logger;
            _influxDbClient = influxDbClient;
            _influxDbSettings = influxDbSettings.Value;
            _analysisSettings = analysisSettings.Value;
            _notificationService = notificationService;
        }

        public async Task<DeviceAnalysisResult> AnalyzeDeviceDataAsync(string deviceId, Dictionary<string, object> deviceData)
        {
            var result = new DeviceAnalysisResult(deviceId, deviceData);
            
            if (deviceData.TryGetValue("isOnline", out var isOnlineObj) && isOnlineObj is bool isOnline)
            {
                if (!isOnline)
                {
                    result.Status = DeviceStatus.Offline;
                    result.Issues.Add("Device is currently offline");
                    return result;
                }
            }

            AnalyzeBatteryLevel(deviceData, result);
            
            AnalyzeSignalStrength(deviceData, result);
            
            await AnalyzeDeviceHeartbeatAsync(deviceId, result);
            
            AnalyzeLocationChanges(deviceData, result);
            
            UpdateOverallStatus(result);
            
            return result;
        }

        public async Task SaveAnalysisHistoryAsync(DeviceAnalysisResult result)
        {
            try
            {
                var point = PointData.Measurement("device_analysis")
                    .Tag("deviceId", result.DeviceId)
                    .Tag("status", result.Status.ToString())
                    .Field("issueCount", result.Issues.Count)
                    .Field("hasIssues", result.HasIssues)
                    .Timestamp(result.Timestamp, WritePrecision.Ns);

                if (result.HasIssues)
                {
                    point = point.Field("issues", string.Join("; ", result.Issues));
                }

                foreach (var metric in result.AnalysisMetrics)
                {
                    if (metric.Value is double doubleValue)
                    {
                        point = point.Field(metric.Key, doubleValue);
                    }
                    else if (metric.Value is int intValue)
                    {
                        point = point.Field(metric.Key, intValue);
                    }
                    else if (metric.Value is bool boolValue)
                    {
                        point = point.Field(metric.Key, boolValue);
                    }
                    else if (metric.Value is string stringValue)
                    {
                        point = point.Field(metric.Key, stringValue);
                    }
                }                
                
                await _influxDbClient.GetWriteApiAsync().WritePointAsync(point, _influxDbSettings.Bucket, _influxDbSettings.Org);
                _logger.LogInformation("Saved analysis result for device {DeviceId} with status {Status}", result.DeviceId, result.Status);
                
                await SendAlertNotificationIfNeededAsync(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save analysis result for device {DeviceId}", result.DeviceId);
            }
        }

        #region Private Analysis Methods        
        private void AnalyzeBatteryLevel(Dictionary<string, object> deviceData, DeviceAnalysisResult result)
        {
            if (deviceData.TryGetValue("batteryLevel", out var batteryObj) && batteryObj is double batteryLevel)
            {
                result.AnalysisMetrics["batteryLevel"] = batteryLevel;
                
                if (batteryLevel <= _analysisSettings.BatteryThresholds.CriticalLevel)
                {
                    result.Issues.Add($"Battery extremely low: {batteryLevel}%");
                    result.AnalysisMetrics["batteryStatus"] = "critical";
                }
                else if (batteryLevel <= _analysisSettings.BatteryThresholds.WarningLevel)
                {
                    result.Issues.Add($"Battery low: {batteryLevel}%");
                    result.AnalysisMetrics["batteryStatus"] = "warning";
                }
                else
                {
                    result.AnalysisMetrics["batteryStatus"] = "normal";
                }
            }
        }        
        
        private void AnalyzeSignalStrength(Dictionary<string, object> deviceData, DeviceAnalysisResult result)
        {
            if (deviceData.TryGetValue("signalStrength", out var signalObj) && signalObj is double signalStrength)
            {
                result.AnalysisMetrics["signalStrength"] = signalStrength;
                
                if (signalStrength <= _analysisSettings.SignalThresholds.CriticalLevel)
                {
                    result.Issues.Add($"Signal very weak: {signalStrength}%");
                    result.AnalysisMetrics["signalStatus"] = "critical";
                }
                else if (signalStrength <= _analysisSettings.SignalThresholds.WarningLevel)
                {
                    result.Issues.Add($"Signal weak: {signalStrength}%");
                    result.AnalysisMetrics["signalStatus"] = "warning";
                }
                else
                {
                    result.AnalysisMetrics["signalStatus"] = "normal";
                }
            }
        }
        
        private async Task AnalyzeDeviceHeartbeatAsync(string deviceId, DeviceAnalysisResult result)
        {
            try
            {
                var query = $"from(bucket: \"{_influxDbSettings.Bucket}\") " +
                           $"|> range(start: -24h) " +
                           $"|> filter(fn: (r) => r._measurement == \"device_status\" and r.deviceId == \"{deviceId}\") " +
                           $"|> last()";
                
                var tables = await _influxDbClient.GetQueryApi().QueryAsync(query, _influxDbSettings.Org);
                
                if (tables.Count > 0 && tables[0].Records.Count > 0)
                {
                    var lastHeartbeat = tables[0].Records[0].GetTimeInDateTime().GetValueOrDefault();                    
                    var timeSinceLastHeartbeat = DateTime.UtcNow - lastHeartbeat;
                    
                    result.AnalysisMetrics["minutesSinceLastHeartbeat"] = timeSinceLastHeartbeat.TotalMinutes;
                    
                    if (timeSinceLastHeartbeat.TotalMinutes > _analysisSettings.HeartbeatTimeouts.CriticalMinutes)
                    {
                        result.Issues.Add($"No data received for {timeSinceLastHeartbeat.TotalMinutes:F1} minutes");
                        result.AnalysisMetrics["heartbeatStatus"] = "critical";
                    }
                    else if (timeSinceLastHeartbeat.TotalMinutes > _analysisSettings.HeartbeatTimeouts.WarningMinutes)
                    {
                        result.Issues.Add($"No data received for {timeSinceLastHeartbeat.TotalMinutes:F1} minutes");
                        result.AnalysisMetrics["heartbeatStatus"] = "warning";
                    }
                    else
                    {
                        result.AnalysisMetrics["heartbeatStatus"] = "normal";
                    }
                }
                else
                {
                    result.Issues.Add("No historical data found for the device");
                    result.AnalysisMetrics["heartbeatStatus"] = "unknown";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to analyze device heartbeat for device {DeviceId}", deviceId);
                result.Issues.Add("Error analyzing device heartbeat");
            }
        }
        
        private void AnalyzeLocationChanges(Dictionary<string, object> deviceData, DeviceAnalysisResult result)
        {
            if (deviceData.TryGetValue("location", out var locationObj) && locationObj is Dictionary<string, object> location)
            {
                if (location.TryGetValue("latitude", out var latObj) && latObj is double latitude &&
                    location.TryGetValue("longitude", out var lonObj) && lonObj is double longitude)
                {
                    result.AnalysisMetrics["latitude"] = latitude;
                    result.AnalysisMetrics["longitude"] = longitude;
                }
            }
        }
        
        private void UpdateOverallStatus(DeviceAnalysisResult result)
        {
            if (result.Status == DeviceStatus.Offline)
            {
                return;
            }
            
            bool hasCritical = result.AnalysisMetrics.Any(m => 
                m.Key.EndsWith("Status") && m.Value.ToString() == "critical");
                
            bool hasWarning = result.AnalysisMetrics.Any(m => 
                m.Key.EndsWith("Status") && m.Value.ToString() == "warning");
            
            if (hasCritical)
            {
                result.Status = DeviceStatus.Critical;
            }
            else if (hasWarning)
            {
                result.Status = DeviceStatus.Warning;
            }
            else
            {
                result.Status = DeviceStatus.Normal;
            }
        }
        
        private async Task SendAlertNotificationIfNeededAsync(DeviceAnalysisResult result)
        {
            if (!result.HasIssues)
            {
                return;
            }

            var priority = result.Status switch
            {
                DeviceStatus.Critical => NotificationPriority.Critical,
                DeviceStatus.Warning => NotificationPriority.High,
                DeviceStatus.Offline => NotificationPriority.High,
                _ => NotificationPriority.Normal
            };

            if (priority == NotificationPriority.Normal)
            {
                return;
            }

            var subject = $"IoT Device Alert: {result.DeviceId}";
            var message = $"Issues detected with device {result.DeviceId}:\n" +
                          $"Status: {result.Status}\n" +
                          $"Issues: {string.Join(", ", result.Issues)}";

            var properties = new Dictionary<string, string>
            {
                { "DeviceId", result.DeviceId },
                { "Status", result.Status.ToString() },
                { "Timestamp", result.Timestamp.ToString("yyyy-MM-dd HH:mm:ss") }
            };

            foreach (var metric in result.AnalysisMetrics)
            {
                if (metric.Key != "issues")
                {
                    properties.Add(metric.Key, metric.Value?.ToString() ?? "N/A");
                }
            }

            await _notificationService.SendNotificationAsync(subject, message, priority, properties);
            _logger.LogInformation("Sent {Priority} notification for device {DeviceId}", priority, result.DeviceId);
        }
        #endregion
    }
}
