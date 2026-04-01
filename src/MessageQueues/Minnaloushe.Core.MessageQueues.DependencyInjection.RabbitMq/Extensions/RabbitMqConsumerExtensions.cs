using Minnaloushe.Core.MessageQueues.DependencyInjection.Extensions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Models;
using Minnaloushe.Core.MessageQueues.DependencyInjection.RabbitMq.Factories;

namespace Minnaloushe.Core.MessageQueues.DependencyInjection.RabbitMq.Extensions;

/// <summary>
/// Extension methods for registering RabbitMQ consumer infrastructure.
/// </summary>
public static class RabbitMqConsumerExtensions
{
    /// <summary>
    /// Registers the RabbitMQ consumer factory for RabbitMQ connections.
    /// Call this AFTER calling AddRabbitMqClientProviders() and/or AddVaultRabbitMqClientProviders().
    /// Automatically supports all RabbitMQ connection types registered in the "rabbit" provider group.
    /// </summary>
    /// <param name="builder">The message queue builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static MessageQueueBuilder AddRabbitMqConsumers(this MessageQueueBuilder builder)
    {
        // Register consumer factory for all RabbitMQ connection types in the "rabbit" group
        var connectionTypes = builder.GetConnectionTypesForGroup("rabbit");
        if (connectionTypes.Count > 0)
        {
            builder.Services.RegisterConsumerFactory(connectionTypes, new RabbitMqConsumerFactory());
        }

        return builder;
    }
}
