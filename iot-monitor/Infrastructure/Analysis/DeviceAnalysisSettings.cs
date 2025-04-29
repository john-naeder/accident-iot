namespace iot_monitor.Infrastructure.Analysis
{
    public class DeviceAnalysisSettings
    {
        public BatteryThresholds BatteryThresholds { get; set; } = new BatteryThresholds();
        public SignalThresholds SignalThresholds { get; set; } = new SignalThresholds();
        public HeartbeatTimeouts HeartbeatTimeouts { get; set; } = new HeartbeatTimeouts();
    }

    public class BatteryThresholds
    {
        public double WarningLevel { get; set; } = 20.0;
        public double CriticalLevel { get; set; } = 10.0;
    }

    public class SignalThresholds
    {
        public double WarningLevel { get; set; } = 30.0;
        public double CriticalLevel { get; set; } = 15.0;
    }

    public class HeartbeatTimeouts
    {
        public double WarningMinutes { get; set; } = 5;
        public double CriticalMinutes { get; set; } = 15;
    }
}
