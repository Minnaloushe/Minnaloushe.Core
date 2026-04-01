using Microsoft.Extensions.DependencyInjection;

namespace Minnaloushe.Core.Toolbox.AsyncInitializer.KeyedInitializer;

internal class KeyedInitializerRegistry(IServiceProvider serviceProvider) : IKeyedInitializerRegistry
{
    public IReadOnlySet<(object, Type)> Registry { get; } = serviceProvider.GetServices<AsyncKeyedServiceDescriptor>()
        .Select(x => (x.Key, x.ServiceType))
        .ToHashSet();
}