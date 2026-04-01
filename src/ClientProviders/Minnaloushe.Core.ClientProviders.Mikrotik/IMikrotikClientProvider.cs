using Minnaloushe.Core.ClientProviders.Abstractions.StaticClientProvider;
using Minnaloushe.Core.Toolbox.AsyncInitializer;
using tik4net;

namespace Minnaloushe.Core.ClientProviders.Mikrotik;

public interface IMikrotikClientProvider : IStaticClientProvider<ITikConnection>, IAsyncInitializer, IAsyncDisposable
{
    Task ReOpenConnectionAsync();
}