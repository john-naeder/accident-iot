using AccidentMonitor.Application.Common.Interfaces;
using AccidentMonitor.Application.Common.Mappings;
using AccidentMonitor.Application.Common.Models;
using AccidentMonitor.Domain.Entities.Accident;
using AccidentMonitor.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AccidentMonitor.Infrastructure.Repositories;
public class AccidentRepository(IApplicationDbContext context) : IAccidentRepository
{
    private readonly IApplicationDbContext _context = context;

    public async Task<AccidentEntity> AddAsync(AccidentEntity entity, CancellationToken cancellationToken)
    {
        var result = await _context.Accidents.AddAsync(entity, cancellationToken);
        return result.Entity;
    }

    public AccidentEntity? Delete(AccidentEntity accidentEntity)
    {
        _context.Accidents.Remove(accidentEntity);
        return accidentEntity;
    }

    public async Task<IEnumerable<AccidentEntity>> GetAllAsync(CancellationToken cancellationToken)
    {
        var accidents = await _context.Accidents.ToListAsync(cancellationToken: cancellationToken);
        return accidents;
    }

    public async Task<AccidentEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var accident = await _context.Accidents
            //.Include(a => a.AccidentDetails)
            .Include(a => a.AccidentInvolved)
            .FirstOrDefaultAsync(a => a.Guid == id, cancellationToken: cancellationToken);
        return accident;
    }

    public AccidentEntity? Update(AccidentEntity entity)
    {
        _context.Accidents.Update(entity);
        return entity;
    }

    public async Task<IEnumerable<AccidentEntity>> GetAllUnresolveAccident(CancellationToken cancellationToken)
    {
        var accidents = await _context.Accidents
            .Where(a => a.ResolvedStatus == AccidentResolvedStatus.Unresolved)
            .ToListAsync(cancellationToken);
        return accidents;
    }

    public async Task<PaginatedList<AccidentEntity>> GetUnresolvedAccidentWithPagination(int pageNumber, int pageSize)
    {
        return await _context.Accidents
            .Where(a => a.ResolvedStatus == AccidentResolvedStatus.Unresolved)
            .PaginatedListAsync(pageNumber, pageSize);
    }

    public Task<PaginatedList<AccidentEntity>> GetAccidentWithPagination(
        int pageNumber, int pageSize,
        bool isAscending = true
        )
    {

        var query = _context.Accidents.AsQueryable();

        if (isAscending)
        {
            query = query.OrderBy(a => a.Timestamp);
        }
        else
        {
            query = query.OrderByDescending(a => a.Timestamp);
        }
        return query.PaginatedListAsync(pageNumber, pageSize);
    }

    public Task<PaginatedList<AccidentEntity>> GetAccidentWithRangeOfDatePagination(DateOnly startDate, DateOnly endDate, int pageNumber, int pageSize, bool isAscending = true)
    {
        var accidents = _context.Accidents
            .Where(a => a.Timestamp.Date >= startDate.ToDateTime(TimeOnly.MinValue) && a.Timestamp.Date <= endDate.ToDateTime(TimeOnly.MinValue));

        if (isAscending)
        {
            accidents = accidents.OrderBy(a => a.Timestamp);
        }
        else
        {
            accidents = accidents.OrderByDescending(a => a.Timestamp);
        }

        return accidents.PaginatedListAsync(pageNumber, pageSize);
    }
}
