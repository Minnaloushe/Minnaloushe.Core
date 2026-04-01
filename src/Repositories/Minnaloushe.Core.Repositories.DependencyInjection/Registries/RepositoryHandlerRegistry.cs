using Minnaloushe.Core.Repositories.DependencyInjection.Models;

namespace Minnaloushe.Core.Repositories.DependencyInjection.Registries;

/// <summary>
/// Default implementation of repository handler registry.
/// Supports composing multiple handlers for the same connection type.
/// </summary>
public class RepositoryHandlerRegistry : IRepositoryHandlerRegistry
{
    private readonly Dictionary<string, Action<RepositoryRegistrationContext>> _handlers = new(StringComparer.OrdinalIgnoreCase);

    public void RegisterHandler(string connectionType, Action<RepositoryRegistrationContext> handler)
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

    public IReadOnlyDictionary<string, Action<RepositoryRegistrationContext>> GetHandlers()
    {
        return _handlers;
    }
}
