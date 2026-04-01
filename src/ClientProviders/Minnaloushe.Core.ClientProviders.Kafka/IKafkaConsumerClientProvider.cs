using Minnaloushe.Core.ClientProviders.Abstractions;
using Minnaloushe.Core.ClientProviders.Kafka.Wrappers;

namespace Minnaloushe.Core.ClientProviders.Kafka;

public interface IKafkaConsumerClientProvider : IClientProvider<IKafkaConsumerClientWrapper>;