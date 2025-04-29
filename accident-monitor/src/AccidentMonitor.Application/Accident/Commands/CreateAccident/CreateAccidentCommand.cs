using System.Text.Json.Serialization;
using AccidentMonitor.Application.Common.Interfaces;
using AccidentMonitor.Domain.Entities.Accident;
using AccidentMonitor.Domain.Enums;
using AccidentMonitor.Domain.Events.AccidentEvents;

namespace AccidentMonitor.Application.Accident.Commands.CreateAccident;
public record CreateAccidentCommand : IRequest<Guid>
{
    [JsonPropertyName("time")]
    public required DateTime Time { get; init; }
    [JsonPropertyName("latitude")]
    public double Latitude { get; init; }
    [JsonPropertyName("longitude")]
    public double Longitude { get; init; }
    [JsonPropertyName("severity")]
    public AccidentSeverity Severity { get; init; }
    [JsonPropertyName("is_blocking_way")]
    public bool IsBlockingWay { get; init; }
}
public class CreateAccidentReportCommandHandler(IUnitOfWork unitOfWork) 
    : IRequestHandler<CreateAccidentCommand, Guid>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    public async Task<Guid> Handle(
        CreateAccidentCommand request, 
        CancellationToken cancellationToken)
    {
        var accident = new AccidentEntity()
        {
            Timestamp = request.Time,
            Latitude = (float)request.Latitude,
            Longitude = (float)request.Longitude,
            IsBlockingWay = request.IsBlockingWay,
            Severity = request.Severity,
            ResolvedStatus = AccidentResolvedStatus.Unresolved
        };

        var accidentDto = AccidentMapper.ToDto(accident);
        accident.AddDomainEvent(new AccidentCreatedEvent(accidentDto));
        await _unitOfWork.AccidentRepository.AddAsync(accident, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return accident.Guid;
    }
}


