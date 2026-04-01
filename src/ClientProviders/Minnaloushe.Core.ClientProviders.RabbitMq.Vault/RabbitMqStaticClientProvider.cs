using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.ClientProviders.Abstractions;
using Minnaloushe.Core.ClientProviders.Abstractions.CredentialsWatcher;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.ServiceDiscovery.Abstractions;
using Minnaloushe.Core.ServiceDiscovery.Routines;
using Minnaloushe.Core.Toolbox.AsyncInitializer;
using Minnaloushe.Core.Toolbox.DictionaryExtensions;
using RabbitMQ.Client;
using VaultSharp;

namespace Minnaloushe.Core.ClientProviders.RabbitMq.Vault;

public class RabbitMqStaticClientProvider(
    IClientProvider<IVaultClient> vaultClientProvider,
    IOptions<MessageQueueOptions> consumerOptions,
    IOptions<VaultService.Options.VaultOptions> vaultOptions,
    ILogger<RabbitMqStaticClientProvider> logger,
    IInfrastructureConventionProvider dependenciesProvider,
    IServiceDiscoveryService? serviceDiscovery = null)
    : IClientProvider<IConnection>, IAsyncInitializer
{
    private readonly ILogger<RabbitMqStaticClientProvider> _logger = logger;
    private readonly RenewableClientHolder<IConnection> _holder = new();

    public Abstractions.ClientLease.IClientLease<IConnection> Acquire()
        => _holder.Acquire();

    private async Task<IConnection> CreateConnection(CancellationToken cancellationToken)
    {
        var vaultClient = vaultClientProvider.Acquire();

        if (!vaultClient.IsInitialized)
        {
            throw new InvalidOperationException("Vault client is not initialized");
        }

        var endpoint = serviceDiscovery is null
            ? null
            : (await serviceDiscovery.ResolveServiceEndpoint(consumerOptions.Value.ServiceName, cancellationToken))
                ?.FirstOrDefault();

        var secret = await vaultClient.Client.V1.Secrets.KeyValue.V2.ReadSecretAsync(
            await dependenciesProvider.GetStaticSecretPath(consumerOptions.Value.ServiceName)
            , mountPoint: vaultOptions.Value.MountPoint);
        var data = secret.Data.Data;

        var host = endpoint?.Host
                   ?? data.GetStringValue("host")
                   ?? throw new NullReferenceException("Failed to get host from vault secret");
        var port = (endpoint?.Port)
                   ?? data.GetUshortValue("port")
                   ?? throw new NullReferenceException("Failed to get port from vault secret");
        var username = data.GetStringValue("username") ?? throw new NullReferenceException("Failed to get username from vault secret");
        var password = data.GetStringValue("password") ?? throw new NullReferenceException("Failed to get password from vault secret");

        var factory = new ConnectionFactory()
        {
            HostName = host,
            Port = port,
            UserName = username,
            Password = password,
        };

        return await factory.CreateConnectionAsync(cancellationToken);
    }

    public async Task<bool> InitializeAsync(CancellationToken cancellationToken)
    {
        _holder.RotateClient(await CreateConnection(cancellationToken));

        return true;
    }
}
