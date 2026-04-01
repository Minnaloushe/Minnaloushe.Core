using Microsoft.Extensions.DependencyInjection;
using Minnaloushe.Core.ClientProviders.Abstractions.CredentialsWatcher;
using Minnaloushe.Core.ClientProviders.Postgres;
using Minnaloushe.Core.Repositories.Abstractions;
using Npgsql;

namespace Minnaloushe.Core.Repositories.Postgres.Factories;

/// <summary>
/// Factory for creating PostgreSQL client providers that use connection strings from configuration.
/// This factory handles connections that specify a ConnectionString for direct PostgreSQL connection.
/// </summary>
public class ConnectionStringPostgresClientProviderFactory(IServiceProvider serviceProvider)
    : IPostgresClientProviderFactory
{
    /// <summary>
    /// Determines if this factory can create a provider.
    /// Returns true when ConnectionString is configured.
    /// </summary>
    public bool CanCreate(RepositoryOptions options)
    {
        return !string.IsNullOrWhiteSpace(options.ConnectionString);
    }

    public IPostgresClientProvider Create(string connectionName)
    {
        // Create connection-specific holder to ensure isolation between providers
        var clientHolder = new RenewableClientHolder<NpgsqlDataSource>();

        // Create the provider with isolated dependencies
        var provider = ActivatorUtilities.CreateInstance<ConnectionStringPostgresClientProvider>(
            serviceProvider,
            connectionName,
            clientHolder);

        return provider;
    }
}
