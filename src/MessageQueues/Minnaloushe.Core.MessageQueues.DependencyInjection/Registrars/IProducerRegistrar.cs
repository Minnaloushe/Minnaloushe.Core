using Microsoft.Extensions.DependencyInjection;

namespace Minnaloushe.Core.MessageQueues.DependencyInjection.Registrars;

/// <summary>
/// Helper interface for type-safe producer registration without reflection.
/// Each provider creates a typed instance to handle registration for specific message types.
/// </summary>
public interface IProducerRegistrar
{
    /// <summary>
    /// Registers all required services for the producer.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="producerName">The name of the producer.</param>
    /// <param name="connectionName">The name of the connection this producer uses.</param>
    /// <param name="keySelector">Optional function to extract a key from the message for partitioning (Kafka).</param>
    void Register(IServiceCollection services, string producerName, string connectionName, object? producerOptions);
}