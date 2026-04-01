using Minnaloushe.Core.MessageQueues.DependencyInjection.Factories;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Registrars;
using Minnaloushe.Core.MessageQueues.Kafka.Producers.Registrars;

namespace Minnaloushe.Core.MessageQueues.Kafka.Producers.Factories;

/// <summary>
/// Producer factory for Kafka message queues.
/// Registers producers for Kafka connections.
/// </summary>
public class KafkaProducerFactory : IProducerFactory
{
    /// <summary>
    /// The connection type identifier for Kafka.
    /// </summary>
    public const string ConnectionType = "kafka";

    /// <summary>
    /// Creates a producer registrar for the specified message type.
    /// </summary>
    public IProducerRegistrar CreateRegistrar(Type messageType)
    {
        var registrarType = typeof(KafkaProducerRegistrar<>).MakeGenericType(messageType);
        return (IProducerRegistrar)Activator.CreateInstance(registrarType)!;
    }
}