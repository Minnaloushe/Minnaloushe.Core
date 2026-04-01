using Minnaloushe.Core.MessageQueues.DependencyInjection.Models;

namespace Minnaloushe.Core.MessageQueues.DependencyInjection.Registries;

/// <summary>
/// Registry for message queue connection type handlers.
/// Allows different provider libraries to register their connection handlers.
/// </summary>
public interface IMessageQueueHandlerRegistry
{
    /// <summary>
    /// Registers a handler for a specific connection type.
    /// </summary>
    /// <param name="connectionType">The connection type (e.g., "rabbitmq", "kafka").</param>
    /// <param name="handler">The handler action to register connections of this type.</param>
    void RegisterHandler(string connectionType, Action<MessageQueueRegistrationContext> handler);

    /// <summary>
    /// Gets all registered handlers.
    /// </summary>
    /// <returns>Dictionary of connection type handlers.</returns>
    IReadOnlyDictionary<string, Action<MessageQueueRegistrationContext>> GetHandlers();
}