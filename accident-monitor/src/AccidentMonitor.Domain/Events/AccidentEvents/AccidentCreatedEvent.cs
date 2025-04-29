
using AccidentMonitor.Domain.Dtos;

namespace AccidentMonitor.Domain.Events.AccidentEvents;
public class AccidentCreatedEvent(AccidentDto createdAccident) : BaseEvent
{
    public AccidentDto CreatedAccident { get; } = createdAccident 
        ?? throw new ArgumentException(nameof(CreatedAccident));
   
}
