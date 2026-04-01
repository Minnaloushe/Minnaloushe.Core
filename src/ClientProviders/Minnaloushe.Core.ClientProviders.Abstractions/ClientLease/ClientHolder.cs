namespace Minnaloushe.Core.ClientProviders.Abstractions.ClientLease;

public sealed class ClientHolder<TClient>(TClient client, long epoch)
{
    private int _refCount = 0;
    public TClient Client { get; } = client;
    public long Epoch { get; init; } = epoch;

    public void Retain()
    {
        Interlocked.Increment(ref _refCount);
    }

    public void Release()
    {
        Interlocked.Decrement(ref _refCount);
    }

    internal CancellationTokenSource TokenSource { get; } = new();

    public CancellationToken CancellationToken => TokenSource.Token;

    public void ReleaseAndDisposeWhenNoRefs()
    {
        if (Interlocked.Decrement(ref _refCount) <= 0)
        {
            (Client as IDisposable)?.Dispose();
            TokenSource.Dispose();
        }
    }
}