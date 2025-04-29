using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace AccidentMonitor.Infrastructure.Caching;
/// <summary>
/// Service for interacting with Redis cache.
/// </summary>
public class RedisCacheService 
{
    private readonly IDistributedCache _cache;
    private readonly DistributedCacheEntryOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisCacheService"/> class.
    /// </summary>
    /// <param name="cache">The distributed cache instance.</param>
    public RedisCacheService(IDistributedCache cache)
    {
        _cache = cache;
        _options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
            SlidingExpiration = TimeSpan.FromMinutes(2)
        };
    }

    /// <summary>
    /// Gets the cached data for the specified key.
    /// </summary>
    /// <typeparam name="T">The type of the cached data.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The cached data or default if not found.</returns>
    public async Task<T?> GetTAsync<T>(string key, CancellationToken cancellationToken)
    {
        var cachedData = await _cache.GetStringAsync(key, cancellationToken);
        return string.IsNullOrEmpty(cachedData) ? default : JsonSerializer.Deserialize<T>(cachedData);
    }

    /// <summary>
    /// Sets the data in the cache with the specified key.
    /// </summary>
    /// <typeparam name="T">The type of the data to cache.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The data to cache.</param>
    /// <param name="options">The cache entry options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task SetAsync<T>(string key, T value, DistributedCacheEntryOptions? options = null, CancellationToken cancellationToken = default) where T : class
    {
        var dataToCache = JsonSerializer.Serialize(value);
        await _cache.SetStringAsync(key, dataToCache, options ?? _options, cancellationToken);
    }

    /// <summary>
    /// Sets the data in the cache with the specified key and absolute expiration.
    /// </summary>
    /// <typeparam name="T">The type of the data to cache.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The data to cache.</param>
    /// <param name="absoluteExpiration">The absolute expiration time.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task SetAsync<T>(string key, T value, TimeSpan absoluteExpiration, CancellationToken cancellationToken = default) where T : class
    {
        var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = absoluteExpiration };
        var dataToCache = JsonSerializer.Serialize(value);
        await _cache.SetStringAsync(key, dataToCache, options, cancellationToken);
    }

    /// <summary>
    /// Removes the cached data for the specified key.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        await _cache.RemoveAsync(key, cancellationToken);
    }

    /// <summary>
    /// Generates a cache key for a product.
    /// </summary>
    /// <param name="id">The product ID.</param>
    /// <returns>The generated cache key.</returns>
    public static string GenerateProductCacheKey(Guid id) => $"product:{id}";
}
