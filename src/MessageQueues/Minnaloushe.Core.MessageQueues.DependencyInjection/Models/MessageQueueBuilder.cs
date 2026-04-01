using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Registries;

namespace Minnaloushe.Core.MessageQueues.DependencyInjection.Models;

/// <summary>
/// Builder for configuring message queue infrastructure.
/// </summary>
/// <param name="Services">The service collection to register services into.</param>
/// <param name="HandlerRegistry">Registry of connection type handlers.</param>
/// <param name="ConnectionTypeRegistry">Registry of supported connection types.</param>
/// <param name="Configuration">The application configuration.</param>
public record MessageQueueBuilder(
    IServiceCollection Services,
    IMessageQueueHandlerRegistry HandlerRegistry,
    IConnectionTypeRegistry ConnectionTypeRegistry,
    IConfiguration Configuration)
{
    /// <summary>
    /// List of consumer registrations added via AddConsumer.
    /// </summary>
    internal List<ConsumerRegistration> ConsumerRegistrations { get; } = [];

    /// <summary>
    /// List of producer registrations added via AddProducer.
    /// </summary>
    public List<ProducerRegistration> ProducerRegistrations { get; } = [];

    /// <summary>
    /// Registers a connection type with its provider group.
    /// Should be called by client provider registration methods.
    /// </summary>
    /// <param name="connectionType">The connection type to register (e.g., "kafka", "kafka-static").</param>
    /// <param name="providerGroup">The provider group identifier (e.g., "kafka", "rabbit").</param>
    public void RegisterConnectionType(string connectionType, string providerGroup)
    {
        ConnectionTypeRegistry.RegisterConnectionType(connectionType, providerGroup);
    }

    /// <summary>
    /// Registers multiple connection types with the same provider group.
    /// </summary>
    /// <param name="connectionTypes">The connection types to register.</param>
    /// <param name="providerGroup">The provider group identifier.</param>
    public void RegisterConnectionTypes(string[] connectionTypes, string providerGroup)
    {
        foreach (var connectionType in connectionTypes)
        {
            ConnectionTypeRegistry.RegisterConnectionType(connectionType, providerGroup);
        }
    }

    /// <summary>
    /// Gets all connection types registered for a specific provider group.
    /// Used by producer and consumer registration to determine which connection types to support.
    /// </summary>
    /// <param name="providerGroup">The provider group identifier (e.g., "kafka", "rabbit").</param>
    /// <returns>All connection types in the specified group.</returns>
    public IReadOnlyCollection<string> GetConnectionTypesForGroup(string providerGroup)
    {
        return ConnectionTypeRegistry.GetConnectionTypesForGroup(providerGroup);
    }

    /// <summary>
    /// Gets all registered connection types.
    /// Used by producer and consumer registration to determine which types to support.
    /// </summary>
    public IReadOnlyCollection<string> GetRegisteredConnectionTypes()
    {
        return ConnectionTypeRegistry.GetRegisteredConnectionTypes();
    }
}