using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.Toolbox.Caching.Options;
using Newtonsoft.Json;

namespace Minnaloushe.Core.Toolbox.Caching.Proxy;

public class DistributedCacheProxy : IDistributedCacheProxy
{
    private readonly IDistributedCache _cache;
    private readonly DistributedCacheOptions _options;
    private readonly Dictionary<int, SemaphoreSlim> _syncLockBucket;

    public DistributedCacheProxy(IOptions<DistributedCacheOptions> options, IDistributedCache cache)
    {
        _cache = cache;
        _options = options.Value;

        _syncLockBucket = Enumerable.Range(0, _options.SyncLockCount)
            .ToDictionary(k => k, _ => new SemaphoreSlim(1, 1));
    }

    private int SyncLockCount => _options.SyncLockCount;

    public async Task<TResponse?> GetValueAsync<TRequest, TResponse>(TRequest request,
        Func<TRequest, CancellationToken, TResponse> responseProducer,
        CancellationToken cancellationToken)
    {
        var requestHash = Math.Abs(request?.GetHashCode() ?? 0);
        var key = typeof(TRequest).Name + requestHash;

        var semaphore = _syncLockBucket[requestHash % SyncLockCount];

        await semaphore.WaitAsync(cancellationToken);
        try
        {
            // Fetch the value from the cache
            var result = await _cache.GetStringAsync(key, cancellationToken);

            if (result != null)
            {
                return JsonConvert.DeserializeObject<TResponse>(result);
            }

            // Generate the response and store it in the cache
            var response = responseProducer(request, cancellationToken);
            var serializedResponse = JsonConvert.SerializeObject(response);

            await _cache.SetStringAsync(key, serializedResponse, _options.CacheEntityOptions, cancellationToken);

            return response;
        }
        finally
        {
            semaphore.Release(); // Ensure the semaphore is released
        }
    }
}