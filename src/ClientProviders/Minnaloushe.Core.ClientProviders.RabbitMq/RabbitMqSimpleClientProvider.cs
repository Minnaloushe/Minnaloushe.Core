using Microsoft.Extensions.Logging;
using Minnaloushe.Core.ClientProviders.Abstractions;
using Minnaloushe.Core.ClientProviders.Abstractions.CredentialsWatcher;
using Minnaloushe.Core.ServiceDiscovery.Abstractions;
using Minnaloushe.Core.Toolbox.AsyncInitializer;
using Minnaloushe.Core.Toolbox.StringExtensions;
using RabbitMQ.Client;

namespace Minnaloushe.Core.ClientProviders.RabbitMq;

public class RabbitMqSimpleClientProvider(
    string connectionName,
    RabbitMqClientOptions options,
    ILogger<RabbitMqSimpleClientProvider> logger,
    IServiceDiscoveryService? serviceDiscovery = null)
    : IClientProvider<IConnection>, IAsyncInitializer
{
    private readonly RenewableClientHolder<IConnection> _holder = new();

    private async Task<IConnection> CreateConnection(CancellationToken cancellationToken)
    {
        var host = options.Host;
        var port = options.Port;
        var username = options.Username;
        var password = options.Password;

        if (options.ServiceName.IsNotNullOrWhiteSpace() && serviceDiscovery != null)
        {
            var endpoint = await serviceDiscovery.ResolveServiceEndpoint(options.ServiceName, cancellationToken);
            host = endpoint.FirstOrDefault()?.Host ?? host;
            port = (endpoint.FirstOrDefault()?.Port) ?? port;
        }

        var factory = new ConnectionFactory()
        {
            HostName = host,
            Port = port,
            UserName = username,
            Password = password,
        };


        var connection = await factory.CreateConnectionAsync(cancellationToken);

        return connection;
    }

    public Abstractions.ClientLease.IClientLease<IConnection> Acquire() => _holder.Acquire();

    public async Task<bool> InitializeAsync(CancellationToken cancellationToken)
    {
        _holder.RotateClient(await CreateConnection(cancellationToken));

        logger.LogInformation("Initialized client for connection {ConnectionName}", connectionName);

        return true;
    }
}