using Minnaloushe.Core.MessageQueues.DependencyInjection.Extensions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Models;
using Minnaloushe.Core.MessageQueues.Kafka.Producers.Factories;

namespace Minnaloushe.Core.MessageQueues.Kafka.Producers.Extensions;

/// <summary>
/// Extension methods for registering Kafka producer infrastructure.
/// </summary>
public static class KafkaProducerExtensions
{
    /// <summary>
    /// Registers the Kafka producer factory and handler for Kafka connections.
    /// Call this AFTER calling AddKafkaClientProviders() and/or AddVaultKafkaClientProviders().
    /// Automatically supports all Kafka connection types registered in the "kafka" provider group.
    /// </summary>
    /// <param name="builder">The message queue builder.</param>
    /// <returns>The builder for chaining.</returns>
    /// <remarks>Producers support topic autocreation, make sure 'auto create topic' is disabled in kafka</remarks>
    public static MessageQueueBuilder AddKafkaProducers(this MessageQueueBuilder builder)
    {
        // Register producer factory for all Kafka connection types in the "kafka" group
        var connectionTypes = builder.GetConnectionTypesForGroup("kafka");
        if (connectionTypes.Count > 0)
        {
            builder.Services.RegisterProducerFactory(connectionTypes, new KafkaProducerFactory());
        }

        return builder;
    }
}
