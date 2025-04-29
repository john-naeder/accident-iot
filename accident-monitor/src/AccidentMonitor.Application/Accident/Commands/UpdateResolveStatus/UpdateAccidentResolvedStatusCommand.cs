using AccidentMonitor.Application.Common.Interfaces;
using AccidentMonitor.Domain.Enums;
using AccidentMonitor.Domain.Events.AccidentEvents;

namespace AccidentMonitor.Application.Accident.Commands.UpdateResolveStatus;
public record UpdateAccidentResolvedStatusCommand : IRequest<Guid?>
{
    public required Guid AccidentId { get; init; }
    public required bool IsResolved { get; init; }
}

public class UpdateAccidentResolvedStatusCommandHandler(IUnitOfWork unitOfWork) : IRequestHandler<UpdateAccidentResolvedStatusCommand, Guid?>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    public async Task<Guid?> Handle(UpdateAccidentResolvedStatusCommand request, CancellationToken cancellationToken)
    {
        var existingAccident = await _unitOfWork.AccidentRepository.GetByIdAsync(request.AccidentId, cancellationToken);
        if (existingAccident == null) return null;
        var status = request.IsResolved
            ? AccidentResolvedStatus.Resolved
            : AccidentResolvedStatus.Unresolved;

        if (existingAccident == null) return Guid.Empty;
        if (existingAccident.ResolvedStatus == status) return request.AccidentId;

        existingAccident.ResolvedStatus = status;

        var accidentDto = AccidentMapper.ToDto(existingAccident);
        existingAccident.AddDomainEvent(new AccidentUpdatedEvent(accidentDto));

        _unitOfWork.AccidentRepository.Update(existingAccident);
        //if (status == AccidentResolvedStatus.Resolved)
        //{
        //    await _unitOfWork.PolygonCoordinateRepository.DeleteResolvedPolygonCoordinates(request.AccidentId, cancellationToken);
        //}
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return request.AccidentId;
    }
}
