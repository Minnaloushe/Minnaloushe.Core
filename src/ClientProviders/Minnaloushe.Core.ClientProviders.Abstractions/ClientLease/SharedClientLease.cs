namespace Minnaloushe.Core.ClientProviders.Abstractions.ClientLease;

public class SharedClientLease<TClient>(TClient client) : IClientLease<TClient>
{
    public TClient Client => client;
    public long Epoch => 1;
    public bool IsInitialized => true;
    public CancellationToken CancellationToken => CancellationToken.None;
    void IDisposable.Dispose()
    {
        // No-op: Shared lease does not manage the lifecycle of the client.
    }
}