using Confluent.Kafka;

namespace Minnaloushe.Core.ClientProviders.Kafka.Wrappers;

public interface IKafkaAdminClientWrapper
{
    IAdminClient Client { get; }
}