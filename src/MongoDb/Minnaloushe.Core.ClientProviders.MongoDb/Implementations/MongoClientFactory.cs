using Minnaloushe.Core.ClientProviders.MongoDb.Abstractions;
using Minnaloushe.Core.ClientProviders.MongoDb.Models;
using Minnaloushe.Core.ServiceDiscovery.Abstractions;
using MongoDB.Driver;

namespace Minnaloushe.Core.ClientProviders.MongoDb.Implementations;

public class MongoClientFactory (
    IServiceDiscoveryService serviceDiscovery,
    IApplicationDependenciesProvider applicationRoutines
    )
    : IMongoClientFactory
{
    public async Task<IMongoClient> CreateAsync(MongoConfig? config)
    {
        ArgumentNullException.ThrowIfNull(config, "Mongo configuration was not provided");

        var hostName = await applicationRoutines.GetConsulServiceName(config.serviceName);
        var host = (await serviceDiscovery.ResolveServiceEndpoint(hostName)
            ).FirstOrDefault();

        if (host == null)
        {
            throw new InvalidOperationException($"Could not resolve service endpoint for MongoDB service '{config.serviceName}'.");
        }

        var settings = MongoClientSettings.FromConnectionString(
            $"mongodb://{config.Username}:{config.Password}@{host.Address}:{host.Port}/{config.Database}?authSource=admin");
        var client = new MongoClient(settings);

        return client;
    }
}