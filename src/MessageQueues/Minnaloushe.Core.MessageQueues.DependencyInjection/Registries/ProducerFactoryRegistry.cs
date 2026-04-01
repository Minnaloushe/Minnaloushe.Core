using Minnaloushe.Core.MessageQueues.DependencyInjection.Factories;

namespace Minnaloushe.Core.MessageQueues.DependencyInjection.Registries;

/// <summary>
/// Default implementation of the producer factory registry.
/// </summary>
public class ProducerFactoryRegistry : IProducerFactoryRegistry
{
    private readonly Dictionary<string, IProducerFactory> _factories = new(StringComparer.OrdinalIgnoreCase);

    public void RegisterFactory(string connectionType, IProducerFactory factory)
    {
        if (_factories.ContainsKey(connectionType))
        {
            throw new InvalidOperationException($"Factory with key {connectionType} has already been registered");
        }
        _factories[connectionType] = factory;
    }

    public IProducerFactory? GetFactory(string connectionType)
    {
        return _factories.GetValueOrDefault(connectionType);
    }
}
