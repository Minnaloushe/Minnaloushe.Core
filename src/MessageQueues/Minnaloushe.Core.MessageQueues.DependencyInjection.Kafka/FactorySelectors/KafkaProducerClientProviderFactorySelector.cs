using Minnaloushe.Core.ClientProviders.Abstractions.FactorySelector;
using Minnaloushe.Core.ClientProviders.Kafka;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Kafka.Factories;

namespace Minnaloushe.Core.MessageQueues.DependencyInjection.Kafka.FactorySelectors;

public class KafkaProducerClientProviderFactorySelector
    : DefaultClientProviderFactorySelector<IKafkaProducerClientProvider, IKafkaProducerClientProviderFactory, MessageQueueOptions>,
        IKafkaProducerClientProviderFactorySelector
{
}