using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Minnaloushe.Core.Toolbox.Caching.ExternalSourceCache;

namespace Minnaloushe.Core.Toolbox.Caching.Tests;

internal class ExternalCacheSutTest(IDistributedCache cache, ILogger logger, IDictionary<string, string> values)
    : ExternalSourceCache<string, string>(cache, logger)
{
    protected override string GetCacheKey(string key)
    {
        return key;
    }

    protected override Task<string> GetValue(string key, CancellationToken ct)
    {
        return Task.FromResult(values.TryGetValue(key, out var val) ? val : string.Empty);
    }
}