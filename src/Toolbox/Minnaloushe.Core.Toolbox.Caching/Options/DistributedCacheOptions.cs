using Microsoft.Extensions.Caching.Distributed;

namespace Minnaloushe.Core.Toolbox.Caching.Options;

public class DistributedCacheOptions
{
    public int SyncLockCount { get; init; }
    public required DistributedCacheEntryOptions CacheEntityOptions { get; init; }
}