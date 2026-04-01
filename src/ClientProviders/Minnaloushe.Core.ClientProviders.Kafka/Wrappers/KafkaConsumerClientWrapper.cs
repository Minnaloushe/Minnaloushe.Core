using Confluent.Kafka;
using Minnaloushe.Core.ClientProviders.Kafka.Options;
using Minnaloushe.Core.Toolbox.StringExtensions;

namespace Minnaloushe.Core.ClientProviders.Kafka.Wrappers;

public class KafkaConsumerClientWrapper : IKafkaConsumerClientWrapper, IAsyncDisposable
{
    public KafkaConsumerClientWrapper(
        KafkaClientOptions options
        )
    {
        //TODO Move hardcoded values to consumer options
        var config = new ConsumerConfig
        {
            BootstrapServers = options.ConnectionString.IsNotNullOrWhiteSpace()
                ? options.ConnectionString
                : $"{options.Host}:{options.Port}",
            GroupId = options.ServiceKey,
            AutoOffsetReset = options.Parameters.AutoOffsetReset,
            EnableAutoCommit = options.Parameters.EnableAutoCommit,
            MaxPollIntervalMs = options.Parameters.MaxPollIntervalMs,
            SessionTimeoutMs = options.Parameters.SessionTimeoutMs,
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

        Consumer = new ConsumerBuilder<byte[], byte[]>(config).Build();
    }

    public IConsumer<byte[], byte[]> Consumer { get; }

    public ValueTask DisposeAsync()
    {
        Consumer.Dispose();

        GC.SuppressFinalize(this);

        return ValueTask.CompletedTask;
    }
}