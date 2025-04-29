using iot_monitor.Application.Enum;
using System;
using System.Collections.Generic;

namespace iot_monitor.Application.Models
{
    public class DeviceAnalysisResult
    {
        public string DeviceId { get; }
        public Dictionary<string, object> RawData { get; }
        public Dictionary<string, object> AnalysisMetrics { get; } = new Dictionary<string, object>();
        public List<string> Issues { get; } = new List<string>();
        public DeviceStatus Status { get; set; } = DeviceStatus.Normal;
        public DateTime Timestamp { get; } = DateTime.UtcNow;
        public bool HasIssues => Issues.Count > 0;

        public DeviceAnalysisResult(string deviceId, Dictionary<string, object> rawData)
        {
            DeviceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
            RawData = rawData ?? throw new ArgumentNullException(nameof(rawData));
        }
    }
}
