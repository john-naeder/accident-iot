namespace iot_monitor.Infrastructure.InfluxDb
{
    public class InfluxDbSettings
    {
        public string Url { get; set; } = "http://localhost:8086";
        public string Token { get; set; } = "";
        public string Org { get; set; } = "accident_monitor";
        public string Bucket { get; set; } = "iot_status";
    }
}
