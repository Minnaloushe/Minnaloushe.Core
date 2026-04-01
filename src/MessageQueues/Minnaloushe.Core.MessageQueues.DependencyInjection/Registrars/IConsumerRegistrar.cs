using Microsoft.Extensions.DependencyInjection;

namespace Minnaloushe.Core.MessageQueues.DependencyInjection.Registrars;

/// <summary>
/// Helper interface for type-safe consumer registration without reflection.
/// Each provider creates a typed instance to handle registration for specific message types.
/// </summary>
public interface IConsumerRegistrar
{
    /// <summary>
    /// Registers all required services for the consumer.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="consumerName">The name of the consumer.</param>
    /// <param name="connectionName">The name of the connection this consumer uses.</param>
    void Register(IServiceCollection services, string consumerName, string connectionName, int parallelism);
}
