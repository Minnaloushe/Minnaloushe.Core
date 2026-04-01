using Microsoft.Extensions.DependencyInjection;
using Minnaloushe.Core.ClientProviders.Abstractions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Extensions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Models;
using Minnaloushe.Core.MessageQueues.DependencyInjection.RabbitMq.Factories;
using Minnaloushe.Core.MessageQueues.DependencyInjection.RabbitMq.FactorySelectors;
using RabbitMQ.Client;

namespace Minnaloushe.Core.MessageQueues.DependencyInjection.RabbitMq.Extensions;

public static class RabbitMqClientProviderExtensions
{
    public static MessageQueueBuilder AddRabbitMqClientProviders(this MessageQueueBuilder builder)
    {
        // Register connection types with the "rabbit" provider group
        builder.RegisterConnectionTypes(["rabbit", "rabbitmq"], "rabbit");

        builder.Services.AddSingleton<IRabbitMqClientProviderFactory, RabbitMqSimpleClientProviderFactory>();
        builder.Services.AddSingleton<IRabbitMqClientProviderFactorySelector, RabbitMqClientProviderFactorySelector>();

        builder.Services.RegisterMessageQueueHandler(["rabbit", "rabbitmq"], context =>
        {
            context.RegisterKeyedProvider<
                IClientProvider<IConnection>,
                IRabbitMqClientProviderFactory,
                IRabbitMqClientProviderFactorySelector>();
        });

        return builder;
    }
}
