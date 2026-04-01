using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Minnaloushe.Core.Toolbox.Caching.ExternalSourceCache;

/// <summary>
/// Provides a base implementation for caching values retrieved from an external source.
/// Uses <see cref="IDistributedCache"/> for distributed caching and ensures thread safety with a semaphore.
/// </summary>
/// <typeparam name="TKey">The type of the cache key.</typeparam>
/// <typeparam name="TValue">The type of the cached value. Must be a reference type.</typeparam>
public abstract class ExternalSourceCache<TKey, TValue>(IDistributedCache cache, ILogger logger)
    : IExternalSourceCache<TKey, TValue> where TValue : class
{
    /// <summary>
    /// Semaphore to ensure only one thread populates the cache for a given key at a time.
    /// </summary>
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    /// Attempts to retrieve a value from the cache. If not present, fetches from the external source and caches the result.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The cached value, or <c>null</c> if retrieval fails.</returns>
    public async Task<TValue?> TryGetAsync(TKey key, CancellationToken ct)
    {
        var cacheKey = GetCacheKey(key);

        var (cacheValue, cacheHit) = await GetCacheInternalAsync(cacheKey, ct);

        if (cacheHit)
        {
            return cacheValue?.Value;
        }

        await _semaphore.WaitAsync(ct);

        try
        {
            if (cacheHit)
            {
                return cacheValue?.Value;
            }

            var value = await GetValue(key, ct);

            await cache.SetStringAsync(cacheKey, JsonConvert.SerializeObject(value), ct);

            return value;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get value");

            await cache.SetStringAsync(cacheKey, string.Empty, ct);
        }
        finally
        {
            _semaphore.Release();
        }

        return null;
    }

    /// <summary>
    /// Generates a cache key string from the provided key.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <returns>A string representation of the cache key.</returns>
    protected abstract string GetCacheKey(TKey key);

    /// <summary>
    /// Retrieves the value from the external source.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The value retrieved from the external source.</returns>
    protected abstract Task<TValue> GetValue(TKey key, CancellationToken ct);

    /// <summary>
    /// Internal method to get the cached value and cache hit status.
    /// </summary>
    /// <param name="key">The cache key string.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A tuple containing the cached value and cache hit status.</returns>
    private async Task<(CacheValue? Cache, bool CacheHit)> GetCacheInternalAsync(string key, CancellationToken ct)
    {
        var stringValue = await cache.GetStringAsync(key, ct);

        if (stringValue == null)
        {
            return (null, false);
        }

        var val = JsonConvert.DeserializeObject<TValue>(stringValue);

        if (stringValue == string.Empty || val == null)
        {
            return (null, true);
        }

        return (new CacheValue(val), true);
    }

    /// <summary>
    /// Represents a cached value.
    /// </summary>
    /// <param name="Value">The cached value.</param>
    protected record CacheValue(TValue Value);
}