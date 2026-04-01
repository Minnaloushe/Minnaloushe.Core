namespace Minnaloushe.Core.ClientProviders.Abstractions.ClientLease;

/// <summary>
/// Provides a lightweight lease that manages the lifetime of a client instance, ensuring proper disposal and resource management.
/// </summary>
/// <remarks>Once disposed, the client lease cannot be used to access the client instance. Attempting to access
/// the client after disposal will throw an ObjectDisposedException. This type is sealed and cannot be
/// inherited.</remarks>
/// <typeparam name="TClient">The type of client instance managed by the lease.</typeparam>
/// <param name="holder">The holder that supplies access to the client instance and controls its lifecycle.</param>
public sealed class ManagedClientLease<TClient>(ClientHolder<TClient> holder) : IClientLease<TClient>
{
    private bool _isDisposed;

    public TClient Client => _isDisposed ? throw new ObjectDisposedException(nameof(ManagedClientLease<>)) : holder.Client;

    public long Epoch => _isDisposed ? throw new ObjectDisposedException(nameof(ManagedClientLease<>)) : holder.Epoch;

    public bool IsInitialized => Epoch > 0;

    public CancellationToken CancellationToken => _isDisposed ? throw new ObjectDisposedException(nameof(ManagedClientLease<>)) : holder.CancellationToken;

    void IDisposable.Dispose()
    {
        if (!_isDisposed)
        {
            holder.Release();
        }

        _isDisposed = true;
    }
}