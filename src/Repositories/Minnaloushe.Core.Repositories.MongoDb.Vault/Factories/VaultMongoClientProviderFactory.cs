using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.ClientProviders.Abstractions.CredentialsWatcher;
using Minnaloushe.Core.ClientProviders.MongoDb;
using Minnaloushe.Core.ClientProviders.MongoDb.Models;
using Minnaloushe.Core.ClientProviders.MongoDb.Vault;
using Minnaloushe.Core.Repositories.Abstractions;
using Minnaloushe.Core.Repositories.MongoDb.Factories;
using Minnaloushe.Core.ServiceDiscovery.Routines;
using MongoDB.Driver;

namespace Minnaloushe.Core.Repositories.MongoDb.Vault.Factories;

/// <summary>
/// Factory for creating MongoDB client providers that use Vault for credential management.
/// This factory handles connections that specify a ServiceName for Vault-based credential retrieval.
/// </summary>
public class VaultMongoClientProviderFactory(IServiceProvider serviceProvider) : IMongoClientProviderFactory
{
    /// <summary>
    /// Determines if this factory can create a provider.
    /// Returns true when ServiceName is configured (indicating Vault-based credentials).
    /// </summary>
    public bool CanCreate(RepositoryOptions options)
    {
        return !string.IsNullOrWhiteSpace(options.ServiceName);
    }

    public IMongoClientProvider Create(string connectionName)
    {
        var clientHolder = new RenewableClientHolder<IMongoClient>();
        var credentialsWatcher = ActivatorUtilities.CreateInstance<LeasedCredentialWatcher<MongoDbCredentials>>(serviceProvider);
        var dependenciesProvider = serviceProvider.GetRequiredService<IInfrastructureConventionProvider>();
        var repositoryOptions = serviceProvider.GetRequiredService<IOptionsMonitor<RepositoryOptions>>().Get(connectionName);

        var provider = ActivatorUtilities.CreateInstance<MongoClientProvider>(
            serviceProvider,
            connectionName,
            clientHolder,
            credentialsWatcher,
            (Func<string, Task<string>>)(serviceName => dependenciesProvider.GetDatabaseRole(serviceName, repositoryOptions.RoleName)));

        return provider;
    }
}
