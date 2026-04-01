using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.ClientProviders.Abstractions.CredentialsWatcher;
using Minnaloushe.Core.ClientProviders.Postgres;
using Minnaloushe.Core.ClientProviders.Postgres.Models;
using Minnaloushe.Core.ClientProviders.Postgres.Vault;
using Minnaloushe.Core.Repositories.Abstractions;
using Minnaloushe.Core.Repositories.Postgres.Factories;
using Minnaloushe.Core.ServiceDiscovery.Routines;
using Npgsql;

namespace Minnaloushe.Core.Repositories.Postgres.Vault.Factories;

/// <summary>
/// Factory for creating PostgreSQL client providers that use Vault for credential management.
/// This factory handles connections that specify a ServiceName for Vault-based credential retrieval.
/// </summary>
public class VaultPostgresClientProviderFactory(IServiceProvider serviceProvider) : IPostgresClientProviderFactory
{
    /// <summary>
    /// Determines if this factory can create a provider.
    /// Returns true when ServiceName is configured (indicating Vault-based credentials).
    /// </summary>
    public bool CanCreate(RepositoryOptions options)
    {
        return !string.IsNullOrWhiteSpace(options.ServiceName);
    }

    public IPostgresClientProvider Create(string connectionName)
    {
        var clientHolder = new RenewableClientHolder<NpgsqlDataSource>();
        var credentialsWatcher = ActivatorUtilities.CreateInstance<LeasedCredentialWatcher<PostgresCredentials>>(serviceProvider);
        var dependenciesProvider = serviceProvider.GetRequiredService<IInfrastructureConventionProvider>();
        var repositoryOptions = serviceProvider.GetRequiredService<IOptionsMonitor<RepositoryOptions>>().Get(connectionName);

        var provider = ActivatorUtilities.CreateInstance<PostgresClientProvider>(
            serviceProvider,
            connectionName,
            clientHolder,
            credentialsWatcher,
            (Func<string, Task<string>>)(serviceName => dependenciesProvider.GetDatabaseRole(serviceName, repositoryOptions.RoleName)));

        return provider;
    }
}
