using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minnaloushe.Core.Toolbox.Caching.Proxy;

namespace Minnaloushe.Core.Toolbox.Caching.Extensions;

public static class DistributedCacheProxyExtensions
{
    public static IServiceCollection RegisterDistributedCacheProxy(IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<DistributedCacheEntryOptions>()
            .BindConfiguration(nameof(DistributedCacheEntryOptions))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IDistributedCacheProxy, DistributedCacheProxy>();

        return services;
    }
}