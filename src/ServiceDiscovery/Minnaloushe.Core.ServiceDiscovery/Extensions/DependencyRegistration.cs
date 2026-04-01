using Consul;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.ServiceDiscovery.Abstractions;
using Minnaloushe.Core.ServiceDiscovery.Consul;
using Minnaloushe.Core.ServiceDiscovery.Options;
using Minnaloushe.Core.ServiceDiscovery.Routines;

namespace Minnaloushe.Core.ServiceDiscovery.Extensions;

public static class DependencyRegistration
{
    public static IServiceCollection AddServiceDiscovery(this IServiceCollection services)
    {
        services.AddOptions<ServiceDiscoveryOptions>()
            .BindConfiguration(ServiceDiscoveryOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();


        services.AddSingleton<IServiceDiscoveryService, ConsulServiceDiscovery>();

        services.AddSingleton<IConsulClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ServiceDiscoveryOptions>>().Value;

            return new ConsulClient(new ConsulClientConfiguration()
            {
                Address = new Uri($"http://{options.ConsulService}:{options.ConsulPort}")
            });
        });

        services.AddSingleton<IInfrastructureConventionProvider, InfrastructureConventionProvider>();

        return services;
    }
}