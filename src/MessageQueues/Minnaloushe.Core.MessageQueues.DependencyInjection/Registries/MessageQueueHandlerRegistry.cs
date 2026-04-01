using Minnaloushe.Core.MessageQueues.DependencyInjection.Models;

namespace Minnaloushe.Core.MessageQueues.DependencyInjection.Registries;

/// <summary>
/// Default implementation of message queue handler registry.
/// Supports composing multiple handlers for the same connection type.
/// </summary>
public class MessageQueueHandlerRegistry : IMessageQueueHandlerRegistry
{
    private readonly Dictionary<string, Action<MessageQueueRegistrationContext>> _handlers = new(StringComparer.OrdinalIgnoreCase);

    public void RegisterHandler(string connectionType, Action<MessageQueueRegistrationContext> handler)
    {
        if (string.IsNullOrWhiteSpace(connectionType))
        {
            throw new ArgumentException("Connection type cannot be null or empty.", nameof(connectionType));
        }

        ArgumentNullException.ThrowIfNull(handler);

        if (_handlers.TryGetValue(connectionType, out var existingHandler))
        {
            // Compose the existing handler with the new one
            _handlers[connectionType] = context =>
            {
                existingHandler(context);
                handler(context);
            };
        }
        else
        {
            _handlers[connectionType] = handler;
        }
    }

    public IReadOnlyDictionary<string, Action<MessageQueueRegistrationContext>> GetHandlers()
    {
        return _handlers;
    }
}
