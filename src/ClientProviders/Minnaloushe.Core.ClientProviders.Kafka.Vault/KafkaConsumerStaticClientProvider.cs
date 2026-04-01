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

public class KafkaConsumerStaticClientProvider(
    string connectionString,
    IClientProvider<IVaultClient> vaultClientProvider,
    KafkaClientOptions options,
    IOptions<VaultService.Options.VaultOptions> vaultOptions,
    IServiceDiscoveryService serviceDiscovery,
    IInfrastructureConventionProvider dependenciesProvider,
    ILogger<KafkaConsumerStaticClientProvider> logger)
    : IKafkaConsumerClientProvider, IAsyncInitializer
{
    private readonly RenewableClientHolder<IKafkaConsumerClientWrapper> _holder = new();
    private KafkaClientOptions _options = options;
    public Abstractions.ClientLease.IClientLease<IKafkaConsumerClientWrapper> Acquire()
        => _holder.Acquire();

    private async Task<IKafkaConsumerClientWrapper?> CreateClient(CancellationToken cancellationToken)
    {
        var vaultClient = vaultClientProvider.Acquire();

        if (!vaultClient.IsInitialized)
        {
            logger.VaultClientNotInitialized();
            return null;
        }

        var endpoint = (await serviceDiscovery.ResolveServiceEndpoint(_options.ServiceName, cancellationToken))
            .FirstOrDefault();

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

        return new KafkaConsumerClientWrapper(_options);
    }

    public async Task<bool> InitializeAsync(CancellationToken cancellationToken)
    {
        var client = await CreateClient(cancellationToken);

        if (client == null)
        {
            return false;
        }

        _holder.RotateClient(client);

        logger.InitializedClient(connectionString);

        return true;
    }
}