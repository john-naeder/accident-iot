using AccidentMonitor.Domain.Entities.MapStuff.Polygons;

namespace AccidentMonitor.Application.Common.Interfaces;
public interface IBlockedPolygonCoordRepository : IRepository<PolygonCoordinate>
{
    Task<IEnumerable<PolygonCoordinate>> DeleteResolvedPolygonCoordinatesAsync(Guid accidentId, CancellationToken cancellationToken = default);
    Task<IEnumerable<PolygonCoordinate>> GetAllUnResolvedAsync(CancellationToken cancellationToken = default);
}
