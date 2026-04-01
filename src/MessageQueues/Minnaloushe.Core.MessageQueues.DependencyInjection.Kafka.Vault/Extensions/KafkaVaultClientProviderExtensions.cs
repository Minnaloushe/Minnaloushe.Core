using Microsoft.Extensions.DependencyInjection;
using Minnaloushe.Core.ClientProviders.Kafka;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Extensions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Kafka.Factories;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Kafka.FactorySelectors;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Kafka.Vault.Factories;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Models;

namespace Minnaloushe.Core.MessageQueues.DependencyInjection.Kafka.Vault.Extensions;

public static class KafkaVaultClientProviderExtensions
{
    public static MessageQueueBuilder AddVaultKafkaClientProviders(this MessageQueueBuilder builder)
    {
        builder.RegisterConnectionTypes(["kafka-static"], "kafka");

        builder.Services.AddSingleton<IKafkaConsumerClientProviderFactory, KafkaConsumerStaticClientProviderFactory>();
        builder.Services.AddSingleton<IKafkaConsumerClientProviderFactorySelector, KafkaConsumerClientProviderFactorySelector>();

        builder.Services.AddSingleton<IKafkaAdminClientProviderFactory, KafkaAdminStaticClientProviderFactory>();
        builder.Services.AddSingleton<IKafkaAdminClientProviderFactorySelector, KafkaAdminClientProviderFactorySelector>();

        builder.Services.AddSingleton<IKafkaProducerClientProviderFactory, KafkaProducerStaticClientProviderFactory>();
        builder.Services.AddSingleton<IKafkaProducerClientProviderFactorySelector, KafkaProducerClientProviderFactorySelector>();

        builder.Services.RegisterMessageQueueHandler(["kafka-static"], context =>
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
