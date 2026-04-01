using Minnaloushe.Core.Repositories.DependencyInjection.Models;

namespace Minnaloushe.Core.Repositories.DependencyInjection.Registries;

/// <summary>
/// Registry for repository connection type handlers.
/// Allows different provider libraries to register their connection handlers.
/// </summary>
public interface IRepositoryHandlerRegistry
{
    /// <summary>
    /// Registers a handler for a specific connection type.
    /// </summary>
    /// <param name="connectionType">The connection type (e.g., "mongodb", "postgres").</param>
    /// <param name="handler">The handler action to register connections of this type.</param>
    void RegisterHandler(string connectionType, Action<RepositoryRegistrationContext> handler);

    /// <summary>
    /// Gets all registered handlers.
    /// </summary>
    /// <returns>Dictionary of connection type handlers.</returns>
    IReadOnlyDictionary<string, Action<RepositoryRegistrationContext>> GetHandlers();
}
