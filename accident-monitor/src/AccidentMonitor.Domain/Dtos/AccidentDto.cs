namespace AccidentMonitor.Domain.Dtos;

public class AccidentDto
{
    public Guid Guid { get; set; } = Guid.Empty;
    public DateTime Timestamp { get; set; } = DateTime.MinValue;
    public double Longitude { get; set; } 
    public double Latitude { get; set; }
    public AccidentSeverity Severity { get; set; } = AccidentSeverity.Low;
    public AccidentResolvedStatus ResolvedStatus { get; set; } = AccidentResolvedStatus.Unresolved;
    public bool IsBlockingWay { get; set; } = true;
}
