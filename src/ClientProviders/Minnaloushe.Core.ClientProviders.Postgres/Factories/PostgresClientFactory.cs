using Minnaloushe.Core.ClientProviders.Postgres.Models;
using Minnaloushe.Core.ServiceDiscovery.Abstractions;
using Minnaloushe.Core.ServiceDiscovery.Routines;
using Npgsql;

namespace Minnaloushe.Core.ClientProviders.Postgres.Factories;

public class PostgresClientFactory(
    IServiceDiscoveryService serviceDiscovery,
    IInfrastructureConventionProvider applicationRoutines)
    : IPostgresClientFactory
{
    public async Task<NpgsqlDataSource> CreateAsync(PostgresConfig? config, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(config, "Postgres configuration was not provided");

        var hostName = await applicationRoutines.GetConsulServiceName(config.ServiceName);
        var host = (await serviceDiscovery.ResolveServiceEndpoint(hostName, cancellationToken))
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"Could not resolve service endpoint for PostgreSQL service '{config.ServiceName}'.");

        var connectionString = $"Host={host.Host};Port={host.Port};Database={config.Database};Username={config.Username};Password={config.Password}";
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        var dataSource = dataSourceBuilder.Build();

        return dataSource;
    }
}
