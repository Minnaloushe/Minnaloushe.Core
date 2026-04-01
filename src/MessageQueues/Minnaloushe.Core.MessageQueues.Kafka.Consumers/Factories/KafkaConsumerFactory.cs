using Minnaloushe.Core.MessageQueues.DependencyInjection.Factories;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Registrars;
using Minnaloushe.Core.MessageQueues.Kafka.Consumers.Registrars;

namespace Minnaloushe.Core.MessageQueues.Kafka.Consumers.Factories;

/// <summary>
/// Consumer factory for Kafka message queues.
/// Registers initializers, engine factories, and hosted services for Kafka consumers.
/// </summary>
public class KafkaConsumerFactory : IConsumerFactory
{
    /// <summary>
    /// The connection type identifier for Kafka.
    /// </summary>
    public const string ConnectionType = "kafka";

    /// <summary>
    /// Creates a consumer registrar for the specified message type.
    /// </summary>
    public IConsumerRegistrar CreateRegistrar(Type messageType)
    {
        var registrarType = typeof(KafkaConsumerRegistrar<>).MakeGenericType(messageType);
        return (IConsumerRegistrar)Activator.CreateInstance(registrarType)!;
    }
}