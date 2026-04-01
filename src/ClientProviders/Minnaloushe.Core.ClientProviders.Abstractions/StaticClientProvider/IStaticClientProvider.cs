namespace Minnaloushe.Core.ClientProviders.Abstractions.StaticClientProvider;

public interface IStaticClientProvider<out TClient> where TClient : class
{
    TClient Client { get; }
}