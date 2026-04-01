namespace Minnaloushe.Core.ClientProviders.Abstractions.ClientLease;

public interface IClientLease<out TClient> : IDisposable
{
    TClient Client { get; }
    long Epoch { get; }
    bool IsInitialized { get; }
    CancellationToken CancellationToken { get; }
}