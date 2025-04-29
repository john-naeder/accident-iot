using System.Text.Json;
using AccidentMonitor.Application.Common.Interfaces;
using AccidentMonitor.Application.Common.Models;
using AccidentMonitor.Domain.Entities.Accident;
using AccidentMonitor.Domain.Enums;
using AccidentMonitor.Infrastructure.Repositories;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace AccidentMonitor.Infrastructure.Caching.Accident;
public class CachingAccidentRepositoryDecorator : IAccidentRepository
{
    private readonly AccidentRepository _decorated;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDistributedCache _cache;
    private readonly ILogger<CachingAccidentRepositoryDecorator> _logger;
    private static readonly DistributedCacheEntryOptions DefaultCacheOptions =
        new DistributedCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(5));

    public CachingAccidentRepositoryDecorator(
        AccidentRepository decorated,
        IUnitOfWork unitOfWork,
        IDistributedCache distributedCache,
        ILogger<CachingAccidentRepositoryDecorator> logger)
    {
        _cache = distributedCache;
        _decorated = decorated;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<AccidentEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        string cacheKey = RedisCacheService.GenerateProductCacheKey(id);
        AccidentEntity? accidentEntity = null;

        try
        {
            var cachedAccident = await _cache.GetStringAsync(cacheKey, cancellationToken);
            if (!string.IsNullOrEmpty(cachedAccident))
            {
                accidentEntity = JsonSerializer.Deserialize<AccidentEntity>(cachedAccident);
                return accidentEntity;
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Error while getting accident from cache: {ex.Message}", ex);
        }

        accidentEntity = await _decorated.GetByIdAsync(id, cancellationToken);
        if (accidentEntity != null)
        {
            try
            {
                var productJson = JsonSerializer.Serialize(accidentEntity);
                await _cache.SetStringAsync(cacheKey, productJson, DefaultCacheOptions, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error while setting accident in cache: {ex.Message}", ex);
            }
        }
        return accidentEntity;
    }

    public Task<IEnumerable<AccidentEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _decorated.GetAllAsync(cancellationToken);
    }

    public async Task<AccidentEntity> AddAsync(AccidentEntity entity, CancellationToken cancellationToken = default)
    {
        
        return await _decorated.AddAsync(entity, cancellationToken);
    }

    public AccidentEntity? Update(AccidentEntity entity)
    {
        return _decorated.Update(entity);
    }

    public AccidentEntity? Delete(AccidentEntity entity)
    {
        
        return _decorated.Delete(entity);
    }

    public Task<PaginatedList<AccidentEntity>> GetUnresolvedAccidentWithPagination(int pageNumber, int pageSize)
    {
        return _decorated.GetUnresolvedAccidentWithPagination(pageNumber, pageSize);
    }

    public Task<PaginatedList<AccidentEntity>> GetAccidentWithPagination(int pageNumber, int pageSize, bool isAscending = true)
    {
        return _decorated.GetAccidentWithPagination(pageNumber, pageSize, isAscending); 
    }

    public Task<PaginatedList<AccidentEntity>> GetAccidentWithRangeOfDatePagination(DateOnly startDate, DateOnly endDate, int pageNumber, int pageSize, bool isAscending = true)
    {
        return _decorated.GetAccidentWithRangeOfDatePagination(startDate, endDate, pageNumber, pageSize, isAscending);
    }

    public Task<IEnumerable<AccidentEntity>> GetAllUnresolveAccident(CancellationToken cancellationToken)
    {
        return _decorated.GetAllUnresolveAccident(cancellationToken);
    }
}
