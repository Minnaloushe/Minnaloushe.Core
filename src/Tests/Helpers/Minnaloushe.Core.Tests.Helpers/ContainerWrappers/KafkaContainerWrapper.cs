using Minnaloushe.Core.Toolbox.TestHelpers;
using Testcontainers.Kafka;

namespace Minnaloushe.Core.Tests.Helpers.ContainerWrappers;

public class KafkaContainerWrapper : ContainerWrapperBase<KafkaBuilder, KafkaContainer, KafkaConfiguration>
{
    protected override KafkaBuilder InitContainer(KafkaBuilder builder)
    {
        return builder.WithEnvironment("KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR", "1")
            .WithEnvironment("KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR", "1")
            .WithEnvironment("KAFKA_TRANSACTION_STATE_LOG_MIN_ISR", "1")

            // Enable topic deletion for test cleanup
            .WithEnvironment("KAFKA_DELETE_TOPIC_ENABLE", "true")

            // Disable auto topic creation - topics must be created explicitly
            .WithEnvironment("KAFKA_AUTO_CREATE_TOPICS_ENABLE", "false")

            // disable metrics to reduce startup noise
            .WithEnvironment("CONFLUENT_METRICS_ENABLE", "false")

            // JAAS config for PLAIN/SASL (used by Confluent images)
            .WithEnvironment("KAFKA_LISTENER_NAME_PLAINTEXT_PLAIN_SASL_JAAS_CONFIG",
                $"org.apache.kafka.common.security.plain.PlainLoginModule required " +
                $"username=\"{Username}\" " +
                $"password=\"{Password}\" " +
                $"user_{Username}=\"{Password}\";");
    }

    protected override string ImageName => "confluentinc/cp-kafka:7.9.0";
    protected override ushort ContainerPort => 9092;
    protected override KafkaBuilder CreateBuilder() => new(Image.FromDefaultRegistry(ImageName));
}