using Minnaloushe.Core.MessageQueues.DependencyInjection.Factories;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Registrars;

namespace Minnaloushe.Core.MessageQueues.RabbitMq.Producers;

/// <summary>
/// Producer factory for RabbitMQ message queues.
/// Registers producers for RabbitMQ connections.
/// </summary>
internal class RabbitMqProducerFactory : IProducerFactory
{
    /// <summary>
    /// The connection type identifier for RabbitMQ.
    /// </summary>
    public const string ConnectionType = "rabbitmq";

    /// <summary>
    /// Creates a producer registrar for the specified message type.
    /// </summary>
    public IProducerRegistrar CreateRegistrar(Type messageType)
    {
        var registrarType = typeof(RabbitMqProducerRegistrar<>).MakeGenericType(messageType);
        return (IProducerRegistrar)Activator.CreateInstance(registrarType)!;
    }
}