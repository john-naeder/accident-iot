using System.Text.Json;
using AccidentMonitor.Domain.Events.AccidentEvents;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace AccidentMonitor.Infrastructure.Caching.Accident;
public class AccidentCacheEventHandler :
    INotificationHandler<AccidentCreatedEvent>,
    INotificationHandler<AccidentDeletedEvent>,
    INotificationHandler<AccidentUpdatedEvent>
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<AccidentCacheEventHandler> _logger;
    private static readonly DistributedCacheEntryOptions DefaultCacheOptions =
        new DistributedCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(10));

    public AccidentCacheEventHandler(
        IDistributedCache cache,
        ILogger<AccidentCacheEventHandler> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task Handle(AccidentCreatedEvent notification, CancellationToken cancellationToken)
    {
        var cacheKey = RedisCacheService.GenerateProductCacheKey(notification.CreatedAccident.Guid);
        try
        {
            var productJson = JsonSerializer.Serialize(notification.CreatedAccident);
            await _cache.SetStringAsync(cacheKey, productJson, DefaultCacheOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while setting accident in cache");
        }
    }

    public async Task Handle(AccidentDeletedEvent notification, CancellationToken cancellationToken)
    {
        var cacheKey = RedisCacheService.GenerateProductCacheKey(notification.AccidentId);
        try
        {
            await _cache.RemoveAsync(cacheKey, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while removing accident from cache");
        }
    }
    public async Task Handle(AccidentUpdatedEvent notification, CancellationToken cancellationToken)
    {
        var cacheKey = RedisCacheService.GenerateProductCacheKey(notification.UpdatedAccident.Guid);
        try
        {
            var productJson = JsonSerializer.Serialize(notification.UpdatedAccident.Guid);
            await _cache.SetStringAsync(cacheKey, productJson, DefaultCacheOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while updating accident in cache");
        }
    }
}
