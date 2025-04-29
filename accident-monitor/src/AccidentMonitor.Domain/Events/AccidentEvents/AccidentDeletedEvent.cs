namespace AccidentMonitor.Domain.Events.AccidentEvents;
public class AccidentDeletedEvent(Guid accidentId) : BaseEvent
{
    public Guid AccidentId { get; } = accidentId;
}
