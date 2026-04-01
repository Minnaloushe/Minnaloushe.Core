using Confluent.Kafka;
using Minnaloushe.Core.ClientProviders.Kafka.Options;
using Minnaloushe.Core.ServiceDiscovery.Entities;
using Minnaloushe.Core.Toolbox.StringExtensions;

namespace Minnaloushe.Core.ClientProviders.Kafka.Wrappers;

public class KafkaAdminClientWrapper : IKafkaAdminClientWrapper, IAsyncDisposable
{
    public KafkaAdminClientWrapper(
        KafkaClientOptions options,
        ServiceEndpoint? endpoint = null
        )
    {
        var config = new AdminClientConfig
        {
            BootstrapServers = options.ConnectionString.IsNotNullOrWhiteSpace()
                ? options.ConnectionString
                : $"{endpoint?.Host ?? options.Host}:{endpoint?.Port ?? options.Port}",
            SecurityProtocol = SecurityProtocol.Plaintext,
            SaslMechanism = SaslMechanism.Plain,
            SaslUsername = options.Username,
            SaslPassword = options.Password,
        };

        Client = new AdminClientBuilder(config).Build();
    }

    public IAdminClient Client { get; }

    public ValueTask DisposeAsync()
    {
        Client.Dispose();

        GC.SuppressFinalize(this);

        return ValueTask.CompletedTask;
    }
}