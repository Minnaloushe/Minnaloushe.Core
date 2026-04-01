using Minnaloushe.Core.ClientProviders.Abstractions.ClientLease;

namespace Minnaloushe.Core.ClientProviders.Abstractions;

public interface IClientProvider<out TClient> where TClient : class
{
    IClientLease<TClient> Acquire();
}