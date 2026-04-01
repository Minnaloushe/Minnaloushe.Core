using Minnaloushe.Core.MessageQueues.DependencyInjection.Factories;

namespace Minnaloushe.Core.MessageQueues.DependencyInjection.Registries;

/// <summary>
/// Registry for producer factories.
/// Each message queue provider (RabbitMQ, Kafka, etc.) registers its producer factory implementation.
/// </summary>
public interface IProducerFactoryRegistry
{
    /// <summary>
    /// Registers a producer factory for a specific connection type.
    /// </summary>
    /// <param name="connectionType">The connection type (e.g., "rabbitmq", "kafka").</param>
    /// <param name="factory">The producer factory for this connection type.</param>
    void RegisterFactory(string connectionType, IProducerFactory factory);

    /// <summary>
    /// Gets the producer factory for a specific connection type.
    /// </summary>
    /// <param name="connectionType">The connection type.</param>
    /// <returns>The producer factory if registered, null otherwise.</returns>
    IProducerFactory? GetFactory(string connectionType);
}