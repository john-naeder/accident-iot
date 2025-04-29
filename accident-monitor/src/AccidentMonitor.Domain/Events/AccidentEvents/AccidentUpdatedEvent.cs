using AccidentMonitor.Domain.Dtos;

namespace AccidentMonitor.Domain.Events.AccidentEvents;
public class AccidentUpdatedEvent(AccidentDto updatedAccident) : BaseEvent
{
    public AccidentDto UpdatedAccident { get; } = updatedAccident 
        ?? throw new ArgumentNullException(nameof(updatedAccident));
}
