using AccidentMonitor.Application.Common.Interfaces;
using AccidentMonitor.Domain.Entities.MapStuff.Polygons;
using Microsoft.EntityFrameworkCore;

namespace AccidentMonitor.Infrastructure.Repositories;
public class BlockedPolygonCoordinateRepository(IApplicationDbContext context) : IBlockedPolygonCoordRepository
{
    private readonly IApplicationDbContext _context = context;
    public async Task<PolygonCoordinate> AddAsync(PolygonCoordinate entity, CancellationToken cancellationToken = default)
    {
        await _context.PolygonCoordinates.AddAsync(entity, cancellationToken);
        return entity;
    }

    public PolygonCoordinate? Delete(PolygonCoordinate existingCoordinate)
    {
        _context.PolygonCoordinates.Remove(existingCoordinate);
        return existingCoordinate;
    }

    public async Task<IEnumerable<PolygonCoordinate>> DeleteResolvedPolygonCoordinatesAsync(Guid accidentId, CancellationToken cancellationToken = default)
    {
        var polygonCoordinates = await _context.PolygonCoordinates
            .Where(p => p.AccidentId == accidentId)
            .ToListAsync(cancellationToken: cancellationToken);

        _context.PolygonCoordinates.RemoveRange(polygonCoordinates);
        return polygonCoordinates;
    }

    public async Task<IEnumerable<PolygonCoordinate>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var polygonCoordinates = await _context.PolygonCoordinates
            .Include(p => p.Accident)
            .ToListAsync(cancellationToken: cancellationToken);
        return polygonCoordinates;
    }

    public async Task<IEnumerable<PolygonCoordinate>> GetAllUnResolvedAsync(CancellationToken cancellationToken = default)
    {
        var polygonCoordinates = await _context.PolygonCoordinates
            .Include(p => p.Accident)
            .Where(p => p.Accident.ResolvedStatus == Domain.Enums.AccidentResolvedStatus.Unresolved)
            .ToListAsync(cancellationToken: cancellationToken);
        return polygonCoordinates;
    }

    public async Task<PolygonCoordinate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var polygonCoordinate = await _context.PolygonCoordinates
            .Include(p => p.Accident)
            .FirstOrDefaultAsync(p => p.Guid == id, cancellationToken: cancellationToken);
        return polygonCoordinate;
    }

    public PolygonCoordinate? Update(PolygonCoordinate existingCoordinate)
    {
        _context.PolygonCoordinates.Update(existingCoordinate);
        return existingCoordinate;
    }
}
