using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccidentMonitor.Application.Common.Interfaces;
public interface IUnitOfWork : IDisposable
{
    IAccidentRepository AccidentRepository { get; } 
    IBlockedPolygonCoordRepository PolygonCoordRepository { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
