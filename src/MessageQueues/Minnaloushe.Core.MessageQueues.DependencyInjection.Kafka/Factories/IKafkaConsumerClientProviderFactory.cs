using Minnaloushe.Core.ClientProviders.Abstractions.Factories;
using Minnaloushe.Core.ClientProviders.Kafka;
using Minnaloushe.Core.MessageQueues.Abstractions;

namespace Minnaloushe.Core.MessageQueues.DependencyInjection.Kafka.Factories;

public interface IKafkaConsumerClientProviderFactory : IClientProviderFactory<IKafkaConsumerClientProvider, MessageQueueOptions>;