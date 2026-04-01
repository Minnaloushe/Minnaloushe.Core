using Microsoft.Extensions.Logging;
using Minnaloushe.Core.ClientProviders.Abstractions.ClientLease;
using Minnaloushe.Core.ClientProviders.Abstractions.CredentialsWatcher;
using Minnaloushe.Core.ClientProviders.Kafka.Options;
using Minnaloushe.Core.ClientProviders.Kafka.Wrappers;
using Minnaloushe.Core.ServiceDiscovery.Abstractions;
using Minnaloushe.Core.ServiceDiscovery.Entities;
using Minnaloushe.Core.Toolbox.AsyncInitializer;
using Minnaloushe.Core.Toolbox.StringExtensions;

namespace Minnaloushe.Core.ClientProviders.Kafka;

public class KafkaProducerSimpleClientProvider(
    string connectionName,
    KafkaClientOptions options,
    ILogger<KafkaProducerSimpleClientProvider> logger,
    IServiceDiscoveryService? serviceDiscovery = null)
    : IKafkaProducerClientProvider, IAsyncInitializer
{
    private readonly RenewableClientHolder<IKafkaProducerClientWrapper> _holder = new();
    private KafkaClientOptions _options = options;

    public IClientLease<IKafkaProducerClientWrapper> Acquire()
        => _holder.Acquire();

    private async Task<IKafkaProducerClientWrapper> CreateClient(CancellationToken cancellationToken)
    {

        ServiceEndpoint? endpoint = null;
        if (_options.ServiceName.IsNotNullOrWhiteSpace() && serviceDiscovery != null)
        {
            endpoint =
                (await serviceDiscovery.ResolveServiceEndpoint(_options.ServiceName, cancellationToken))
                ?.FirstOrDefault();
        }

        _options = _options with
        {
            Host = endpoint?.Host ?? _options.Host,
            Port = endpoint?.Port ?? _options.Port,
        };

        return new KafkaProducerClientWrapper(_options);
    }

    public async Task<bool> InitializeAsync(CancellationToken cancellationToken)
    {
        _holder.RotateClient(await CreateClient(cancellationToken));

        logger.LogInformation("Initialized client for connection {ConnectionName}", connectionName);

        return true;
    }
}
