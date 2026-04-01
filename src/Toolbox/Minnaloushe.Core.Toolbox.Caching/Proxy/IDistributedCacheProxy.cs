namespace Minnaloushe.Core.Toolbox.Caching.Proxy;

public interface IDistributedCacheProxy
{
    Task<TResponse?> GetValueAsync<TRequest, TResponse>(TRequest request,
        Func<TRequest, CancellationToken, TResponse> responseProducer,
        CancellationToken cancellationToken);
}