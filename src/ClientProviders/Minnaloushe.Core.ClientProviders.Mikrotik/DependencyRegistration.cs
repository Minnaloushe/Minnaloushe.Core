using Microsoft.Extensions.DependencyInjection;
using Minnaloushe.Core.ClientProviders.Abstractions.VaultExtensions;

namespace Minnaloushe.Core.ClientProviders.Mikrotik;

public static class DependencyRegistration
{
    public static IServiceCollection AddMikrotikClientProvider(this IServiceCollection services)
    {
        services.AddClientProvider<IMikrotikClientProvider, MikrotikClientProvider, MikrotikOptions>(
            MikrotikOptions.SectionName);

        services.AddSingleton<IMikrotikConnectionFactory, MikrotikConnectionFactory>();

        return services;
    }
}