using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minnaloushe.Core.Repositories.DependencyInjection.Models;
using Minnaloushe.Core.Repositories.DependencyInjection.Registries;
using Minnaloushe.Core.Toolbox.JsonConfiguration;

namespace Minnaloushe.Core.Repositories.DependencyInjection.Extensions;

/// <summary>
/// Extension methods for registering repository client provider handlers.
/// Provides common logic for MongoDB, PostgreSQL, and other database providers.
/// </summary>
public static class RepositoryHandlerExtensions
{
    /// <param name="services">The service collection to which the repository services will be added. Cannot be null.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds repository services and configuration to the specified service collection.
        /// </summary>
        /// <remarks>This method registers the required services for repository functionality, including
        /// configuration and handler registration. Call this method during application startup to enable repository
        /// support.</remarks>
        /// <param name="configuration">The application configuration used to configure repository services. Cannot be null.</param>
        /// <returns>A builder that can be used to further configure repository services.</returns>
        public RepositoryBuilder AddRepositories(IConfiguration configuration)
        {
            services.AddJsonConfiguration();

            var handlerRegistry = services.GetOrCreateRepositoryHandlerRegistry();

            return new RepositoryBuilder(services, handlerRegistry, configuration);
        }

        /// <summary>
        /// Registers a repository handler for the specified connection types in the dependency injection container.
        /// </summary>
        /// <remarks>This method enables multiple repository connection types to be associated with a single
        /// handler registration. Each connection type in <paramref name="connectionTypes"/> will be registered with the
        /// provided <paramref name="handler"/>.</remarks>
        /// <param name="connectionTypes">A collection of connection type names for which the handler will be registered. Cannot be null or contain null
        /// elements.</param>
        /// <param name="handler">An action that configures the repository handler for each specified connection type. Cannot be null.</param>
        /// <returns>The same <see cref="IServiceCollection"/> instance so that additional calls can be chained.</returns>
        public IServiceCollection RegisterRepositoryHandler(IEnumerable<string> connectionTypes,
            Action<RepositoryRegistrationContext> handler)
        {
            var registry = services.GetOrCreateRepositoryHandlerRegistry();

            foreach (var connectionType in connectionTypes)
            {
                registry.RegisterHandler(connectionType, handler);
            }

            return services;
        }

        /// <summary>
        /// Registers a repository handler for the specified connection type in the dependency injection container.
        /// </summary>
        /// <param name="connectionType">The type of repository connection for which the handler is being registered. Cannot be null or empty.</param>
        /// <param name="handler">A delegate that configures the repository handler using the provided registration context. Cannot be null.</param>
        /// <returns>The same instance of <see cref="IServiceCollection"/> that was provided, to allow for method chaining.</returns>
        public IServiceCollection RegisterRepositoryHandler(string connectionType,
            Action<RepositoryRegistrationContext> handler)
        {
            return services.RegisterRepositoryHandler([connectionType], handler);
        }

        /// <summary>
        /// Gets or creates the repository handler registry.
        /// </summary>
        internal IRepositoryHandlerRegistry GetOrCreateRepositoryHandlerRegistry()
        {
            var registryDescriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(IRepositoryHandlerRegistry));

            if (registryDescriptor == null)
            {
                var registry = new RepositoryHandlerRegistry();
                services.AddSingleton<IRepositoryHandlerRegistry>(registry);
                return registry;
            }

            return registryDescriptor.ImplementationInstance as IRepositoryHandlerRegistry ?? throw new InvalidOperationException(
                "IRepositoryHandlerRegistry is registered but not as a singleton instance. " +
                "Ensure it's registered using AddSingleton with an instance.");
        }
    }
}
