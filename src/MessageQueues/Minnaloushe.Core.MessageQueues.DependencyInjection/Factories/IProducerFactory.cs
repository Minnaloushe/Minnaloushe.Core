using Minnaloushe.Core.MessageQueues.DependencyInjection.Registrars;

namespace Minnaloushe.Core.MessageQueues.DependencyInjection.Factories;

/// <summary>
/// Factory interface for creating message producers.
/// Implemented by each message queue provider (RabbitMQ, Kafka, etc.).
/// </summary>
public interface IProducerFactory
{
    /// <summary>
    /// Creates a producer registrar for the specified message type.
    /// The registrar handles type-safe registration without reflection.
    /// </summary>
    /// <param name="messageType">The type of message the producer handles.</param>
    /// <returns>A producer registrar for the message type.</returns>
    IProducerRegistrar CreateRegistrar(Type messageType);
}