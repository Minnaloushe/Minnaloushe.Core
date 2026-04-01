using Confluent.Kafka;

namespace Minnaloushe.Core.ClientProviders.Kafka.Wrappers;

public interface IKafkaConsumerClientWrapper
{
    IConsumer<byte[], byte[]> Consumer { get; }
}