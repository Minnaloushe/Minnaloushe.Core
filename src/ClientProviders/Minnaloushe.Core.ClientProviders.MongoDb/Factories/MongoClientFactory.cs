using Minnaloushe.Core.ClientProviders.MongoDb.Models;
using Minnaloushe.Core.ServiceDiscovery.Abstractions;
using Minnaloushe.Core.ServiceDiscovery.Routines;
using MongoDB.Driver;

namespace Minnaloushe.Core.ClientProviders.MongoDb.Factories;

public class MongoClientFactory(
    IServiceDiscoveryService serviceDiscovery,
    IInfrastructureConventionProvider applicationRoutines
    )
    : IMongoClientFactory
{
    public async Task<IMongoClient> CreateAsync(MongoConfig? config, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(config, "Mongo configuration was not provided");

        var hostName = await applicationRoutines.GetConsulServiceName(config.ServiceName);
        var host = (await serviceDiscovery.ResolveServiceEndpoint(hostName, cancellationToken)
                   ).FirstOrDefault()
                   ?? throw new InvalidOperationException($"Could not resolve service endpoint for MongoDB service '{config.ServiceName}'.");

        var settings = MongoClientSettings.FromConnectionString(
            $"mongodb://{config.Username}:{config.Password}@{host.Host}:{host.Port}/{config.Database}?authSource=admin");
        var client = new MongoClient(settings);

        return client;
    }
}