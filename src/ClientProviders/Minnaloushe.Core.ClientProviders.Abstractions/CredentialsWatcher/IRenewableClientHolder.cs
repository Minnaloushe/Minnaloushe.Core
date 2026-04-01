namespace Minnaloushe.Core.ClientProviders.Abstractions.CredentialsWatcher;

/// <summary>
/// Provides an abstraction for managing and rotating instances of a client type, allowing the client to be seamlessly refreshed or
/// replaced when needed.
/// </summary>
/// <remarks>Implementations should ensure that any resources held by the client are properly released or disposed
/// of when the client is rotated. This interface is useful in scenarios where clients maintain state or connections
/// that require periodic renewal, such as for security, reliability, or resource management purposes.</remarks>
/// <typeparam name="TClient">The type of client managed by this holder. Must be a reference type.</typeparam>
public interface IRenewableClientHolder<TClient> : IClientProvider<TClient> where TClient : class
{
    void RotateClient(TClient client);
}