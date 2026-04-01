using Minnaloushe.Core.MessageQueues.DependencyInjection.Factories;
using Minnaloushe.Core.MessageQueues.DependencyInjection.RabbitMq.Registrars;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Registrars;

namespace Minnaloushe.Core.MessageQueues.DependencyInjection.RabbitMq.Factories;

/// <summary>
/// Consumer factory for RabbitMQ message queues.
/// Registers initializers, engine factories, and hosted services for RabbitMQ consumers.
/// </summary>
public class RabbitMqConsumerFactory : IConsumerFactory
{
    /// <summary>
    /// The connection type identifier for RabbitMQ.
    /// </summary>
    public const string ConnectionType = "rabbitmq";

    /// <summary>
    /// Creates a consumer registrar for the specified message type.
    /// </summary>
    public IConsumerRegistrar CreateRegistrar(Type messageType)
    {
        var registrarType = typeof(RabbitMqConsumerRegistrar<>).MakeGenericType(messageType);
        return (IConsumerRegistrar)Activator.CreateInstance(registrarType)!;
    }
}