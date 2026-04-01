namespace Minnaloushe.Core.MessageQueues.DependencyInjection.Registries;

/// <summary>
/// Registry for tracking connection types and their provider groups.
/// This allows producer and consumer factories to be registered for all connection types
/// within a provider group (e.g., all Kafka variants: "kafka", "kafka-static", "kafka-dynamic").
/// </summary>
public interface IConnectionTypeRegistry
{
    /// <summary>
    /// Registers a connection type with its provider group.
    /// Called by client provider registration methods (e.g., AddKafkaClientProviders).
    /// </summary>
    /// <param name="connectionType">The connection type identifier (e.g., "kafka", "kafka-static").</param>
    /// <param name="providerGroup">The provider group identifier (e.g., "kafka", "rabbit").</param>
    void RegisterConnectionType(string connectionType, string providerGroup);

    /// <summary>
    /// Gets all registered connection types.
    /// </summary>
    /// <returns>A collection of all registered connection types.</returns>
    IReadOnlyCollection<string> GetRegisteredConnectionTypes();

    /// <summary>
    /// Gets all connection types registered for a specific provider group.
    /// Used by producer and consumer registration methods to register factories for all types in the group.
    /// </summary>
    /// <param name="providerGroup">The provider group identifier (e.g., "kafka", "rabbit").</param>
    /// <returns>A collection of connection types in the specified group.</returns>
    IReadOnlyCollection<string> GetConnectionTypesForGroup(string providerGroup);
}
