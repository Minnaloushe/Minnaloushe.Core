using Consul;
using Microsoft.Extensions.Logging;
using Minnaloushe.Core.ServiceDiscovery.Abstractions;
using Minnaloushe.Core.ServiceDiscovery.Entities;

namespace Minnaloushe.Core.ServiceDiscovery.Consul;

public class ConsulServiceDiscovery(
    IConsulClient consulClient,
    ILogger<ConsulServiceDiscovery> logger
) : IServiceDiscoveryService
{
    public async Task<IReadOnlyCollection<ServiceEndpoint>> ResolveServiceEndpoint(string serviceName, CancellationToken cancellationToken)
    {
        try
        {
            var services = (await consulClient.Health.Service(serviceName, tag: null, passingOnly: true, ct: cancellationToken)).Response;

            return services == null || services.Length == 0
                ? []
                : [.. services.Select(s => new ServiceEndpoint
                {
                    Host = s.Service.Address,
                    Port = (ushort)s.Service.Port
                })];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to resolve service endpoint for {ServiceName}", serviceName);
            return [];
        }
    }
}