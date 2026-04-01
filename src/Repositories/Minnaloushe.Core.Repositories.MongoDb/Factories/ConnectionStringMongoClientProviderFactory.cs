using Microsoft.Extensions.DependencyInjection;
using Minnaloushe.Core.ClientProviders.Abstractions.CredentialsWatcher;
using Minnaloushe.Core.ClientProviders.MongoDb;
using Minnaloushe.Core.Repositories.Abstractions;
using MongoDB.Driver;

namespace Minnaloushe.Core.Repositories.MongoDb.Factories;

/// <summary>
/// Factory for creating MongoDB client providers that use connection strings from configuration.
/// This factory handles connections that specify a ConnectionString for direct MongoDB connection.
/// </summary>
public class ConnectionStringMongoClientProviderFactory(IServiceProvider serviceProvider) : IMongoClientProviderFactory
{
    /// <summary>
    /// Determines if this factory can create a provider.
    /// Returns true when ConnectionString is configured.
    /// </summary>
    public bool CanCreate(RepositoryOptions options)
    {
        return !string.IsNullOrWhiteSpace(options.ConnectionString);
    }

    public IMongoClientProvider Create(string connectionName)
    {
        // Create connection-specific holder to ensure isolation between providers
        var clientHolder = new RenewableClientHolder<IMongoClient>();

        // Create the provider with isolated dependencies
        var provider = ActivatorUtilities.CreateInstance<ConnectionStringMongoClientProvider>(
            serviceProvider,
            connectionName,
            clientHolder);

        return provider;
    }
}
