using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.ClientProviders.Abstractions;
using Minnaloushe.Core.ClientProviders.Abstractions.CredentialsWatcher;
using Minnaloushe.Core.ClientProviders.Kafka.Options;
using Minnaloushe.Core.ClientProviders.Kafka.Wrappers;
using Minnaloushe.Core.ServiceDiscovery.Abstractions;
using Minnaloushe.Core.ServiceDiscovery.Routines;
using Minnaloushe.Core.Toolbox.AsyncInitializer;
using Minnaloushe.Core.Toolbox.DictionaryExtensions;
using VaultSharp;

namespace Minnaloushe.Core.ClientProviders.Kafka.Vault;

public class KafkaAdminStaticClientProvider(
    string connectionName,
    IClientProvider<IVaultClient> vaultClientProvider,
    KafkaClientOptions options,
    IOptions<VaultService.Options.VaultOptions> vaultOptions,
    IInfrastructureConventionProvider dependenciesProvider,
    ILogger<KafkaAdminStaticClientProvider> logger,
    IServiceDiscoveryService? serviceDiscovery = null)
    : IKafkaAdminClientProvider, IAsyncInitializer
{
    private readonly RenewableClientHolder<IKafkaAdminClientWrapper> _holder = new();
    private KafkaClientOptions _options = options;
    public Abstractions.ClientLease.IClientLease<IKafkaAdminClientWrapper> Acquire()
        => _holder.Acquire();

    private async Task<IKafkaAdminClientWrapper> CreateClient(CancellationToken cancellationToken)
    {
        var vaultClient = vaultClientProvider.Acquire();

        if (!vaultClient.IsInitialized)
        {
            throw new InvalidOperationException("Vault client is not initialized");
        }

        var endpoint = serviceDiscovery is null
            ? null
            : (await serviceDiscovery.ResolveServiceEndpoint(_options.ServiceName, cancellationToken))?.FirstOrDefault();

        var secret = await vaultClient.Client.V1.Secrets.KeyValue.V2.ReadSecretAsync(
            await dependenciesProvider.GetStaticSecretPath(_options.ServiceName),
            mountPoint: vaultOptions.Value.MountPoint);

        var data = secret.Data.Data;

        var host = endpoint?.Host
                   ?? data.GetStringValue("host")
                   ?? throw new NullReferenceException("Failed to get host from vault secret");
        var port = (endpoint?.Port)
                   ?? data.GetUshortValue("port")
                   ?? throw new NullReferenceException("Failed to get port from vault secret");
        var username = data.GetStringValue("username") ?? throw new NullReferenceException("Failed to get username from vault secret");
        var password = data.GetStringValue("password") ?? throw new NullReferenceException("Failed to get password from vault secret");

        _options = _options with { Host = host, Port = port, Username = username, Password = password };

        return new KafkaAdminClientWrapper(_options);
    }

    public async Task<bool> InitializeAsync(CancellationToken cancellationToken)
    {
        _holder.RotateClient(await CreateClient(cancellationToken));

        logger.InitializedClient(connectionName);

        return true;
    }
}
