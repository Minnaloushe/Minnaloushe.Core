namespace Minnaloushe.Core.Toolbox.Caching.ExternalSourceCache;

public interface IExternalSourceCache<TKey, TValue>
{
    Task<TValue?> TryGetAsync(TKey key, CancellationToken ct);
}