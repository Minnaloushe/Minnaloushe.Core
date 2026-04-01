using Minnaloushe.Core.MessageQueues.DependencyInjection.Registrars;

namespace Minnaloushe.Core.MessageQueues.DependencyInjection.Factories;

/// <summary>
/// Factory interface for creating message consumers.
/// Implemented by each message queue provider (RabbitMQ, Kafka, etc.).
/// </summary>
public interface IConsumerFactory
{
    /// <summary>
    /// Creates a consumer registrar for the specified message type.
    /// The registrar handles type-safe registration without reflection.
    /// </summary>
    /// <param name="messageType">The type of message the consumer handles.</param>
    /// <returns>A consumer registrar for the message type.</returns>
    IConsumerRegistrar CreateRegistrar(Type messageType);
}