using AccidentMonitor.Application.Accident.Dtos;
using AccidentMonitor.Application.Common.Interfaces;
using AccidentMonitor.Domain.Events.AccidentEvents;

namespace AccidentMonitor.Application.Accident.Commands.DeleteAccident;
public record DeleteAccidentCommand : IRequest<Guid?>
{
    public Guid AccidentId { get; init; }
}

public class DeleteAccidentCommandHandler(IUnitOfWork unitOfWork) 
    : IRequestHandler<DeleteAccidentCommand, Guid?>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<Guid?> Handle(DeleteAccidentCommand request, CancellationToken cancellationToken)
    {
        var existingAccident = await _unitOfWork.AccidentRepository.GetByIdAsync(request.AccidentId, cancellationToken);
        if (existingAccident == null) return null;
        var accidentDto = AccidentMapper.ToDto(existingAccident);
        existingAccident.AddDomainEvent(new AccidentDeletedEvent(request.AccidentId));
        _unitOfWork.AccidentRepository.Delete(existingAccident);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return request.AccidentId;
    }
}
