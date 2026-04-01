using Minnaloushe.Core.MessageQueues.DependencyInjection.Factories;

namespace Minnaloushe.Core.MessageQueues.DependencyInjection.Registries;

/// <summary>
/// Default implementation of consumer factory registry.
/// </summary>
public class ConsumerFactoryRegistry : IConsumerFactoryRegistry
{
    private readonly Dictionary<string, IConsumerFactory> _factories = new(StringComparer.OrdinalIgnoreCase);

    public void RegisterFactory(string connectionType, IConsumerFactory factory)
    {
        if (string.IsNullOrWhiteSpace(connectionType))
        {
            throw new ArgumentException("Connection type cannot be null or empty.", nameof(connectionType));
        }

        ArgumentNullException.ThrowIfNull(factory);

        if (!_factories.TryAdd(connectionType, factory))
        {
            throw new InvalidOperationException($"A consumer factory for connection type '{connectionType}' is already registered.");
        }
    }

    public IConsumerFactory? GetFactory(string connectionType)
    {
        return _factories.GetValueOrDefault(connectionType);
    }
}
