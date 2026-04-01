namespace Minnaloushe.Core.ClientProviders.Abstractions.ClientLease;

public class StandAloneLease<TClient>(TClient client) : IClientLease<TClient>
{
    public TClient Client => client;
    public long Epoch => 1;
    public bool IsInitialized => true;
    public CancellationToken CancellationToken => CancellationToken.None;
    void IDisposable.Dispose()
    {
        if (client is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}