using Minnaloushe.Core.ClientProviders.Abstractions;
using Minnaloushe.Core.ClientProviders.Abstractions.Factories;
using Minnaloushe.Core.MessageQueues.Abstractions;
using RabbitMQ.Client;

namespace Minnaloushe.Core.MessageQueues.DependencyInjection.RabbitMq.Factories;

public interface IRabbitMqClientProviderFactory : IClientProviderFactory<IClientProvider<IConnection>, MessageQueueOptions>
{
}