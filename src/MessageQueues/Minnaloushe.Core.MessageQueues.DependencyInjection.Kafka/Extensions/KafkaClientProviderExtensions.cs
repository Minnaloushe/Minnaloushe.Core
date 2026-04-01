using Microsoft.Extensions.DependencyInjection;
using Minnaloushe.Core.ClientProviders.Kafka;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Extensions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Kafka.Factories;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Kafka.FactorySelectors;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Models;

namespace Minnaloushe.Core.MessageQueues.DependencyInjection.Kafka.Extensions;

public static class KafkaClientProviderExtensions
{
    public static MessageQueueBuilder AddKafkaClientProviders(this MessageQueueBuilder builder)
    {
        builder.RegisterConnectionType("kafka", "kafka");

        builder.Services.AddSingleton<IKafkaConsumerClientProviderFactory, KafkaConsumerSimpleClientProviderFactory>();
        builder.Services.AddSingleton<IKafkaConsumerClientProviderFactorySelector, KafkaConsumerClientProviderFactorySelector>();

        builder.Services.AddSingleton<IKafkaAdminClientProviderFactory, KafkaAdminSimpleClientProviderFactory>();
        builder.Services.AddSingleton<IKafkaAdminClientProviderFactorySelector, KafkaAdminClientProviderFactorySelector>();

        builder.Services.AddSingleton<IKafkaProducerClientProviderFactory, KafkaProducerSimpleClientProviderFactory>();
        builder.Services.AddSingleton<IKafkaProducerClientProviderFactorySelector, KafkaProducerClientProviderFactorySelector>();

        builder.Services.RegisterMessageQueueHandler("kafka", context =>
        {
            context.RegisterKeyedProvider<
                IKafkaConsumerClientProvider,
                IKafkaConsumerClientProviderFactory,
                IKafkaConsumerClientProviderFactorySelector>();

            context.RegisterKeyedProvider<
                IKafkaAdminClientProvider,
                IKafkaAdminClientProviderFactory,
                IKafkaAdminClientProviderFactorySelector>();

            context.RegisterKeyedProvider<
                IKafkaProducerClientProvider,
                IKafkaProducerClientProviderFactory,
                IKafkaProducerClientProviderFactorySelector>();
        });

        return builder;
    }
}
