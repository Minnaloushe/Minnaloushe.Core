using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minnaloushe.Core.Repositories.Abstractions;
using Minnaloushe.Core.Repositories.DependencyInjection.Models;
using Minnaloushe.Core.Repositories.DependencyInjection.Registries;

namespace Minnaloushe.Core.Repositories.DependencyInjection.Extensions;

/// <summary>
/// Base configuration extensions for repositories.
/// Parses repository and connection definitions and provides extension points for provider-specific registration.
/// </summary>
public static class RepositoryConfigurationExtensions
{
    /// <summary>
    /// Configures repositories from the provided configuration.
    /// Parses connection and repository definitions, binds named options, and delegates to provider-specific handlers.
    /// Uses handlers registered via IRepositoryHandlerRegistry.
    /// </summary>
    /// <param name="builder">Builder entity used for method chaining</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection Build(this RepositoryBuilder builder)
    {
        var registryDescriptor = builder.Services.FirstOrDefault(sd => sd.ServiceType == typeof(IRepositoryHandlerRegistry));

        if (registryDescriptor?.ImplementationInstance is not IRepositoryHandlerRegistry registry)
        {
            throw new InvalidOperationException(
                "IRepositoryHandlerRegistry not found. Ensure AddMongoDbClientProviders, AddPostgresDbClientProviders, " +
                "or other provider registration methods are called before ConfigureRepositories.");
        }

        var handlers = registry.GetHandlers();

        if (handlers.Count == 0)
        {
            throw new InvalidOperationException(
                "No connection handlers registered. Call AddMongoDbClientProviders, AddPostgresDbClientProviders, " +
                "or register custom handlers before calling ConfigureRepositories.");
        }

        var repoConfigSection = builder.Configuration.GetSection("RepositoryConfiguration");
        var repositories = repoConfigSection.GetSection("Repositories").Get<List<RepositoryDefinition>>() ?? [];

        // Index sections once for fast lookup
        var connectionSections = repoConfigSection.GetSection("Connections").GetChildren()
            .ToDictionary(s => s.GetValue<string>(nameof(ConnectionDefinition.Name))!, StringComparer.OrdinalIgnoreCase);
        var repositorySections = repoConfigSection.GetSection("Repositories").GetChildren()
            .ToDictionary(s => s.GetValue<string>(nameof(RepositoryDefinition.Name))!, StringComparer.OrdinalIgnoreCase);

        // Register repository-scoped named options (repo + its connection)
        RegisterRepositoryOptions(builder.Services, repositories, repositorySections, connectionSections);

        // Register connection-level handlers
        RegisterConnectionInitializers(builder.Services, repositories, connectionSections, handlers);

        return builder.Services;
    }

    /// <summary>
    /// Registers named options for each repository, binding configuration from both repository and connection sections.
    /// </summary>
    private static void RegisterRepositoryOptions(
        IServiceCollection services,
        List<RepositoryDefinition> repositories,
        Dictionary<string, IConfigurationSection> repositorySections,
        Dictionary<string, IConfigurationSection> connectionSections)
    {
        foreach (var repo in repositories)
        {
            if (!repositorySections.TryGetValue(repo.Name, out var repoSection))
            {
                throw new InvalidOperationException($"No configuration section found for repository '{repo.Name}'.");
            }

            if (!connectionSections.TryGetValue(repo.ConnectionName, out var connSection))
            {
                throw new InvalidOperationException($"No matching connection found for repository '{repo.Name}' with connection name '{repo.ConnectionName}'.");
            }

            services.AddOptions<RepositoryOptions>(repo.Name)
                .Bind(connSection)
                .Bind(repoSection)
                .Validate(HasServiceOrConnString, $"Either ServiceName or ConnectionString must be provided in RepositoryOptions for repository '{repo.Name}'.")
                .ValidateOnStart();
        }
    }

    /// <summary>
    /// Registers connection-level named options and delegates to provider-specific handlers for each distinct connection.
    /// </summary>
    private static void RegisterConnectionInitializers(
        IServiceCollection services,
        List<RepositoryDefinition> repositories,
        Dictionary<string, IConfigurationSection> connectionSections,
        IReadOnlyDictionary<string, Action<RepositoryRegistrationContext>> handlers)
    {
        var distinctConnectionNames = repositories
            .Select(r => r.ConnectionName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var connectionName in distinctConnectionNames)
        {
            if (!connectionSections.TryGetValue(connectionName, out var connSection))
            {
                throw new InvalidOperationException($"No configuration section found for connection '{connectionName}'.");
            }

            // Named options for the connection (used by provider creation)
            services.AddOptions<RepositoryOptions>(connectionName)
                .Bind(connSection)
                .Validate(HasServiceOrConnString, $"Either ServiceName or ConnectionString must be provided in RepositoryOptions for connection '{connectionName}'.")
                .ValidateOnStart();

            var type = connSection.GetValue<string>(nameof(ConnectionDefinition.Type));
            var repositoriesForConnection = repositories.Where(c => c.ConnectionName.Equals(connectionName, StringComparison.OrdinalIgnoreCase)).ToList();

            if (!handlers.TryGetValue(type ?? string.Empty, out var handler))
            {
                throw new NotSupportedException($"Connection type '{type}' is not supported for connection '{connectionName}'. No handler registered.");
            }

            var context = new RepositoryRegistrationContext(
                services,
                connectionName,
                connSection,
                repositoriesForConnection);

            handler(context);
        }
    }

    /// <summary>
    /// Validates that either ServiceName or ConnectionString is provided.
    /// </summary>
    private static bool HasServiceOrConnString(RepositoryOptions o)
    {
        return !string.IsNullOrWhiteSpace(o.ServiceName) || !string.IsNullOrWhiteSpace(o.ConnectionString);
    }
}
