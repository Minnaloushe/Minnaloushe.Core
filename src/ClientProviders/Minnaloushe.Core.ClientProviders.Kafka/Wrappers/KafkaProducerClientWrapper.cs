using Confluent.Kafka;
using Minnaloushe.Core.ClientProviders.Kafka.Options;
using Minnaloushe.Core.Toolbox.StringExtensions;

namespace Minnaloushe.Core.ClientProviders.Kafka.Wrappers;

public class KafkaProducerClientWrapper : IKafkaProducerClientWrapper, IAsyncDisposable
{
    public KafkaProducerClientWrapper(
        KafkaClientOptions options
        )
    {
        var config = new ProducerConfig
        {
            BootstrapServers = options.ConnectionString.IsNotNullOrWhiteSpace()
                ? options.ConnectionString
                : $"{options.Host}:{options.Port}",
        };

        // Only configure SASL when username is provided. This avoids setting
        // sasl.mechanism without a matching security.protocol which causes
        // rdkafka configuration warnings.
        if (!string.IsNullOrWhiteSpace(options.Username))
        {
            config.SecurityProtocol = SecurityProtocol.Plaintext;
            config.SaslMechanism = SaslMechanism.Plain;
            config.SaslUsername = options.Username;
            config.SaslPassword = options.Password;
        }
        else
        {
            config.SecurityProtocol = SecurityProtocol.Plaintext;
        }

        Producer = new ProducerBuilder<byte[], byte[]>(config).Build();
    }

    public IProducer<byte[], byte[]> Producer { get; }

    public ValueTask DisposeAsync()
    {
        Producer.Dispose();

        GC.SuppressFinalize(this);

        return ValueTask.CompletedTask;
    }
}