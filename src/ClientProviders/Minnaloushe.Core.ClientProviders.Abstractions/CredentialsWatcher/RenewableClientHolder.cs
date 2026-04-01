using Minnaloushe.Core.ClientProviders.Abstractions.ClientLease;

namespace Minnaloushe.Core.ClientProviders.Abstractions.CredentialsWatcher;

public class RenewableClientHolder<TClient> : IRenewableClientHolder<TClient>, IDisposable where TClient : class
{
    private volatile ClientHolder<TClient> _holder = new(null!, 0);
    private bool _isDisposed = false;
    // ReSharper disable once StaticMemberInGenericType
    private static long _epoch = 0;
    public IClientLease<TClient> Acquire()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, typeof(RenewableClientHolder<TClient>));

        var current = _holder;

        current.Retain();

        return new ManagedClientLease<TClient>(current);
    }

    public void RotateClient(TClient client)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, typeof(RenewableClientHolder<TClient>));

        Interlocked.Increment(ref _epoch);

        var old = Interlocked.Exchange(ref _holder, new ClientHolder<TClient>(client, _epoch));

        old.TokenSource.Cancel();

        old.ReleaseAndDisposeWhenNoRefs();
    }


    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        var old = Interlocked.Exchange(ref _holder!, null);

        old?.ReleaseAndDisposeWhenNoRefs();

        GC.SuppressFinalize(this);
    }
}