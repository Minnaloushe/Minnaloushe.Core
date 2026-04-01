using Minnaloushe.Core.ClientProviders.Abstractions;
using Minnaloushe.Core.ClientProviders.Abstractions.FactorySelector;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.RabbitMq.Factories;
using RabbitMQ.Client;

namespace Minnaloushe.Core.MessageQueues.DependencyInjection.RabbitMq.FactorySelectors;

public class RabbitMqClientProviderFactorySelector
    : DefaultClientProviderFactorySelector<IClientProvider<IConnection>, IRabbitMqClientProviderFactory, MessageQueueOptions>,
        IRabbitMqClientProviderFactorySelector
{
}