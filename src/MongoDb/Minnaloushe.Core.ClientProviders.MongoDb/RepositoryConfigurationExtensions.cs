using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minnaloushe.Core.ClientProviders.Abstractions;
using Minnaloushe.Core.ClientProviders.MongoDb.Abstractions;
using Minnaloushe.Core.ClientProviders.MongoDb.Implementations;

namespace Minnaloushe.Core.ClientProviders.MongoDb;

/// <summary>
/// Provides extension methods for configuring repository services using application configuration settings.
/// </summary>
/// <remarks>This class enables the registration and configuration of repository and connection options, as well
/// as the setup of required initializers for supported data providers. It is intended to be used during application
/// startup to streamline the setup of data access layers based on configuration. The extension methods support named
/// options binding and validation to ensure that each repository and connection is properly configured before
/// use.</remarks>
public static class RepositoryConfigurationExtensions
{
    /// <summary>
    /// Configures repositories from the provided configuration.
    /// Parses connection and repository definitions, binds named options, and registers async initializers.
    /// </summary>
    /// <param name="services">The service collection to register services into.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection ConfigureRepositories(this IServiceCollection services, IConfiguration configuration)
    {
        var repoConfigSection = configuration.GetSection("RepositoryConfiguration");
        var connections = repoConfigSection.GetSection("Connections").Get<List<ConnectionDefinition>>() ?? [];
        var repositories = repoConfigSection.GetSection("Repositories").Get<List<RepositoryDefinition>>() ?? [];

        // Index sections once for fast lookup
        var connectionSections = repoConfigSection.GetSection("Connections").GetChildren()
            .ToDictionary(s => s.GetValue<string>(nameof(ConnectionDefinition.Name))!, StringComparer.OrdinalIgnoreCase);
        var repositorySections = repoConfigSection.GetSection("Repositories").GetChildren()
            .ToDictionary(s => s.GetValue<string>(nameof(RepositoryDefinition.Name))!, StringComparer.OrdinalIgnoreCase);

        // Register repository-scoped named options (repo + its connection)
        RegisterRepositoryOptions(services, repositories, repositorySections, connectionSections);

        // Register one initializer per distinct connection (keyed by connection name)
        RegisterConnectionInitializers(services, repositories, connectionSections);

        return services;
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
    /// Registers connection-level named options and async initializers for each distinct connection.
    /// </summary>
    private static void RegisterConnectionInitializers(
        IServiceCollection services,
        List<RepositoryDefinition> repositories,
        Dictionary<string, IConfigurationSection> connectionSections)
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

            //TODO rework to customizable connection handlers
            switch (type)
            {
                case "mongodb":
                    services.AddMongoConnection(connectionName);
                    foreach (var repo in repositories.Where(c => c.ConnectionName == connectionName))
                    {
                        //TODO Add conditional creation. When connection string provided use static provider
                        services.AddKeyedSingleton<IMongoClientProvider>(repo.Name,
                            (sp, key) => sp.GetRequiredKeyedService<MongoClientProvider>(connectionName));
                    }
                    break;
                default:
                    throw new NotSupportedException($"Connection type '{type}' is not supported for connection '{connectionName}'.");
            }
        }
    }

    /// <summary>
    /// Validates that either ServiceName or ConnectionString is provided.
    /// </summary>
    private static bool HasServiceOrConnString(RepositoryOptions o)
    {
        return !string.IsNullOrWhiteSpace(o.ServiceName) || !string.IsNullOrWhiteSpace(o.ConnectionString);
    }

    // ReSharper disable once ClassNeverInstantiated.Local
    private sealed record ConnectionDefinition
    {
        public string Name { get; init; } = string.Empty;
        public string Type { get; init; } = string.Empty;
        public string ConnectionString { get; init; } = string.Empty;
    }

    // ReSharper disable once ClassNeverInstantiated.Local
    private sealed record RepositoryDefinition
    {
        public string Name { get; init; } = string.Empty;
        public string ConnectionName { get; init; } = string.Empty;
    }
}