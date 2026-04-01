using Minnaloushe.Core.MessageQueues.DependencyInjection.Extensions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Models;
using Minnaloushe.Core.Toolbox.RecyclableMemoryStream.Extensions;

namespace Minnaloushe.Core.MessageQueues.RabbitMq.Producers;

/// <summary>
/// Extension methods for registering RabbitMQ producer infrastructure.
/// </summary>
public static class RabbitMqProducerExtensions
{
    /// <summary>
    /// Registers the RabbitMQ producer factory and handler for RabbitMQ connections.
    /// Call this AFTER calling AddRabbitMqClientProviders() and/or AddVaultRabbitMqClientProviders().
    /// Automatically supports all RabbitMQ connection types registered in the "rabbit" provider group.
    /// </summary>
    /// <param name="builder">The message queue builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static MessageQueueBuilder AddRabbitMqProducers(this MessageQueueBuilder builder)
    {
        builder.Services.ConfigureRecyclableStreams();

        // Register producer factory for all RabbitMQ connection types in the "rabbit" group
        var connectionTypes = builder.GetConnectionTypesForGroup("rabbit");
        if (connectionTypes.Count > 0)
        {
            builder.Services.RegisterProducerFactory(connectionTypes, new RabbitMqProducerFactory());
        }

        return builder;
    }
}
