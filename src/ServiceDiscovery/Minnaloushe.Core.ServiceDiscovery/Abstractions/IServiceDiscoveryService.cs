using Minnaloushe.Core.ServiceDiscovery.Entities;

namespace Minnaloushe.Core.ServiceDiscovery.Abstractions;

public interface IServiceDiscoveryService
{
    Task<IReadOnlyCollection<ServiceEndpoint>> ResolveServiceEndpoint(string serviceName, CancellationToken cancellationToken);
}