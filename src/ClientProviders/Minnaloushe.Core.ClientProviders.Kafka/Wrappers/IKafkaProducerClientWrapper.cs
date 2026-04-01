using Confluent.Kafka;

namespace Minnaloushe.Core.ClientProviders.Kafka.Wrappers;

public interface IKafkaProducerClientWrapper
{
    IProducer<byte[], byte[]> Producer { get; }
}