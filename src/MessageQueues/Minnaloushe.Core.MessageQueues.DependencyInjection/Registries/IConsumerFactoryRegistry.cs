using Minnaloushe.Core.MessageQueues.DependencyInjection.Factories;

namespace Minnaloushe.Core.MessageQueues.DependencyInjection.Registries;

/// <summary>
/// Registry for consumer factories.
/// Each message queue provider (RabbitMQ, Kafka, etc.) registers its consumer factory implementation.
/// </summary>
public interface IConsumerFactoryRegistry
{
    /// <summary>
    /// Registers a consumer factory for a specific connection type.
    /// </summary>
    /// <param name="connectionType">The connection type (e.g., "rabbitmq", "kafka").</param>
    /// <param name="factory">The consumer factory for this connection type.</param>
    void RegisterFactory(string connectionType, IConsumerFactory factory);

    /// <summary>
    /// Gets the consumer factory for a specific connection type.
    /// </summary>
    /// <param name="connectionType">The connection type.</param>
    /// <returns>The consumer factory if registered, null otherwise.</returns>
    IConsumerFactory? GetFactory(string connectionType);
}