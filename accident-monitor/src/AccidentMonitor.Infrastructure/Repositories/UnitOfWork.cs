using System;
using AccidentMonitor.Application.Common.Interfaces;
using AccidentMonitor.Infrastructure.Data;

namespace AccidentMonitor.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;
    private IAccidentRepository? _accidentRepository;
    private IBlockedPolygonCoordRepository? _blockedPolygonCoordRepository;
    public UnitOfWork(ApplicationDbContext context) => _context = context;
    public IAccidentRepository AccidentRepository => _accidentRepository ??= new AccidentRepository(_context);
    public IBlockedPolygonCoordRepository PolygonCoordRepository => _blockedPolygonCoordRepository ??= new BlockedPolygonCoordinateRepository(_context);

    public UnitOfWork(
        ApplicationDbContext context,
        IAccidentRepository accidentRepository,
        IBlockedPolygonCoordRepository blockedPolygonCoordRepository
        )
    {
        _context = context;
        _accidentRepository = accidentRepository;
        _blockedPolygonCoordRepository = blockedPolygonCoordRepository;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
