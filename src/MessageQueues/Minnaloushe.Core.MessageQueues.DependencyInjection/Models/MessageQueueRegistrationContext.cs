using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Extensions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Factories;

namespace Minnaloushe.Core.MessageQueues.DependencyInjection.Models;

/// <summary>
/// Context information passed to message queue handlers during registration.
/// </summary>
/// <param name="Services">The service collection to register services into.</param>
/// <param name="ConnectionName">The name of the connection being registered.</param>
/// <param name="ConnectionSection">The configuration section for this connection.</param>
/// <param name="Consumers">The list of consumers using this connection.</param>
/// <param name="ConsumerRegistrations">The list of programmatic consumer registrations for this connection.</param>
/// <param name="ProducerRegistrations">The list of programmatic producer registrations for this connection.</param>
public sealed record MessageQueueRegistrationContext(
    IServiceCollection Services,
    string ConnectionName,
    IConfigurationSection ConnectionSection,
    List<MessageQueueConfigurationExtensions.ConsumerDefinition> Consumers,
    List<ConsumerRegistration> ConsumerRegistrations,
    List<ProducerRegistration> ProducerRegistrations)
{
    /// <summary>
    /// Creates a context with empty registrations list for backwards compatibility.
    /// </summary>
    public MessageQueueRegistrationContext(
        IServiceCollection Services,
        string ConnectionName,
        IConfigurationSection ConnectionSection,
        List<MessageQueueConfigurationExtensions.ConsumerDefinition> Consumers)
        : this(Services, ConnectionName, ConnectionSection, Consumers, [], [])
    {
    }

    /// <summary>
    /// Registers consumers using the provided consumer factory.
    /// Iterates through all programmatic consumer registrations and invokes the factory for each.
    /// </summary>
    /// <param name="factory">The consumer factory to use for registration.</param>
    public void RegisterConsumers(IConsumerFactory factory)
    {
        foreach (var registration in ConsumerRegistrations)
        {
            var consumerDef = Consumers.FirstOrDefault(c => c.Name.Equals(registration.Name, StringComparison.OrdinalIgnoreCase));
            var parallelism = consumerDef?.Parallelism ?? 1;

            var registrar = factory.CreateRegistrar(registration.MessageType);
            registrar.Register(Services, registration.Name!, ConnectionName, parallelism);
        }
    }

    /// <summary>
    /// Registers producers using the provided producer factory.
    /// Iterates through all programmatic producer registrations and invokes the factory for each.
    /// </summary>
    /// <param name="factory">The producer factory to use for registration.</param>
    public void RegisterProducers(IProducerFactory factory)
    {
        foreach (var registration in ProducerRegistrations)
        {
            var registrar = factory.CreateRegistrar(registration.MessageType);
            registrar.Register(Services, registration.Name!, ConnectionName, registration.ProducerOptions);
        }
    }
}
