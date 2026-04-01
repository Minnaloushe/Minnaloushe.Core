using Minnaloushe.Core.MessageQueues.DependencyInjection.Extensions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Models;
using Minnaloushe.Core.MessageQueues.Kafka.Consumers.Factories;

namespace Minnaloushe.Core.MessageQueues.Kafka.Consumers.Extensions;

/// <summary>
/// Extension methods for registering Kafka consumer infrastructure.
/// </summary>
public static class KafkaConsumerExtensions
{
    /// <summary>
    /// Registers the Kafka consumer factory for Kafka connections.
    /// Call this AFTER calling AddKafkaClientProviders() and/or AddVaultKafkaClientProviders().
    /// Automatically supports all Kafka connection types registered in the "kafka" provider group.
    /// </summary>
    /// <param name="builder">The message queue builder.</param>
    /// <returns>The builder for chaining.</returns>
    /// <remarks>Consumers support topic autocreation, make sure 'auto create topic' is disabled in kafka</remarks>
    public static MessageQueueBuilder AddKafkaConsumers(this MessageQueueBuilder builder)
    {
        // Register consumer factory for all Kafka connection types in the "kafka" group
        var connectionTypes = builder.GetConnectionTypesForGroup("kafka");
        if (connectionTypes.Count > 0)
        {
            builder.Services.RegisterConsumerFactory(connectionTypes, new KafkaConsumerFactory());
        }

        return builder;
    }
}
