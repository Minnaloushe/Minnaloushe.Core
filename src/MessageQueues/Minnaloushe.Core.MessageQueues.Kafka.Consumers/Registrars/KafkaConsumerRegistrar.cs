using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.ClientProviders.Abstractions;
using Minnaloushe.Core.ClientProviders.Kafka;
using Minnaloushe.Core.ClientProviders.Kafka.Wrappers;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.Abstractions.ErrorHandling;
using Minnaloushe.Core.MessageQueues.Abstractions.Routines;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Kafka.Factories;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Kafka.FactorySelectors;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Registrars;
using Minnaloushe.Core.MessageQueues.Kafka.Consumers.ErrorHandling;
using Minnaloushe.Core.MessageQueues.Kafka.Consumers.Factories;
using Minnaloushe.Core.Toolbox.AsyncInitializer;

namespace Minnaloushe.Core.MessageQueues.Kafka.Consumers.Registrars;

/// <summary>
/// Type-safe consumer registrar for Kafka consumers.
/// </summary>
internal class KafkaConsumerRegistrar<TMessage> : IConsumerRegistrar
{
    //Ignoring key in lambdas is intensional, key is provided from captured variable
    public void Register(IServiceCollection services, string consumerName, string connectionName, int parallelism)
    {
        // Register keyed client provider aliases from consumer name -> connection name
        // This allows ConsumerHostedService to resolve providers by consumer name

        services.AddKeyedSingleton<IKafkaAdminClientProvider>(consumerName,
            (sp, _) => sp.GetRequiredKeyedService<IKafkaAdminClientProvider>(connectionName));

        // Create a NEW consumer client provider for each consumer (not an alias)
        // Kafka consumers cannot be shared - each needs its own dedicated instance
        services.AddKeyedSingleton<IKafkaConsumerClientProvider>(consumerName, (sp, _) =>
        {
            var options = sp.GetRequiredService<IOptionsMonitor<MessageQueueOptions>>().Get(consumerName);
            var factories = sp.GetServices<IKafkaConsumerClientProviderFactory>();
            var selector = sp.GetRequiredService<IKafkaConsumerClientProviderFactorySelector>();
            var factory = selector.SelectFactory(options, factories)
                          ?? throw new InvalidOperationException(
                              $"No registered IKafkaConsumerClientProviderFactory can handle consumer '{consumerName}'.");
            return factory.Create(consumerName);
        });

        // Register the consumer provider as IAsyncInitializer so it gets initialized automatically
        services.AddSingleton<IAsyncInitializer>(sp =>
        {
            var provider = sp.GetRequiredKeyedService<IKafkaConsumerClientProvider>(consumerName);
            return provider as IAsyncInitializer
                   ?? throw new InvalidOperationException(
                       $"Consumer provider for '{consumerName}' does not implement IAsyncInitializer.");
        });

        // Also register the base interface that ConsumerHostedService expects
        services.AddKeyedSingleton<IClientProvider<IKafkaConsumerClientWrapper>>(consumerName,
            (sp, _) => sp.GetRequiredKeyedService<IKafkaConsumerClientProvider>(consumerName));

        // Register dedicated client providers for each worker (Kafka requires separate connection per worker)
        foreach (var consumerWorkerName in Enumerable.Range(0, parallelism).Select(i => $"{consumerName}{i}").Concat([consumerName]))
        {
            services.AddKeyedSingleton<IKafkaConsumerClientProvider>(consumerWorkerName, (sp, _) =>
            {
                var options = sp.GetRequiredService<IOptionsMonitor<MessageQueueOptions>>().Get(consumerName);
                var factories = sp.GetServices<IKafkaConsumerClientProviderFactory>();
                var selector = sp.GetRequiredService<IKafkaConsumerClientProviderFactorySelector>();
                var factory = selector.SelectFactory(options, factories)
                              ?? throw new InvalidOperationException(
                                  $"No registered IKafkaConsumerClientProviderFactory can handle consumer '{consumerName}'.");
                return factory.Create(consumerName);
            });

            services.AddKeyedSingleton<IClientProvider<IKafkaConsumerClientWrapper>>(consumerWorkerName,
                (sp, _) => sp.GetRequiredKeyedService<IKafkaConsumerClientProvider>(consumerWorkerName));

            services.AddSingleton<IAsyncInitializer>(sp =>
            {
                var provider = sp.GetRequiredKeyedService<IKafkaConsumerClientProvider>(consumerWorkerName);
                return provider as IAsyncInitializer
                       ?? throw new InvalidOperationException(
                           $"Consumer provider for '{consumerWorkerName}' does not implement IAsyncInitializer.");
            });
        }

        services.AddKeyedSingleton<IKafkaProducerClientProvider>(consumerName,
            (sp, _) => sp.GetRequiredKeyedService<IKafkaProducerClientProvider>(connectionName));

        // Register the initializer (keyed by consumer name)
        services.AddKeyedSingleton<IConsumerInitializer>(consumerName, (sp, _) =>
        {
            var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<MessageQueueOptions>>();
            // Use the keyed provider we just registered
            var adminProvider = sp.GetRequiredKeyedService<IKafkaAdminClientProvider>(consumerName);
            var routines = sp.GetRequiredService<IMessageQueueNamingConventionsProvider>();
            var logger = sp.GetRequiredService<ILogger<KafkaConsumerInitializer<TMessage>>>();

            return new KafkaConsumerInitializer<TMessage>(
                consumerName,
                optionsMonitor,
                adminProvider,
                routines,
                logger);
        });

        // Register the dead letter publisher (keyed by consumer name)
        services.AddKeyedSingleton<IDeadLetterPublisher>(consumerName, (sp, _) =>
        {
            var adminProvider = sp.GetRequiredKeyedService<IKafkaAdminClientProvider>(consumerName);
            var producerProvider = sp.GetRequiredKeyedService<IKafkaProducerClientProvider>(consumerName);
            var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<MessageQueueOptions>>();
            var logger = sp.GetRequiredService<ILogger<KafkaDeadLetterPublisher>>();
            return new KafkaDeadLetterPublisher(consumerName, adminProvider, producerProvider, optionsMonitor, logger);
        });

        // Register the error handling strategy factory (keyed by consumer name)
        services.AddKeyedSingleton<IErrorHandlingStrategyFactory>(consumerName, (sp, _) =>
        {
            var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<MessageQueueOptions>>();
            var routines = sp.GetRequiredService<IMessageQueueNamingConventionsProvider>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var deadLetterPublisher = sp.GetRequiredKeyedService<IDeadLetterPublisher>(consumerName);

            return new KafkaErrorHandlingStrategyFactory<TMessage>(
                optionsMonitor,
                routines,
                deadLetterPublisher,
                loggerFactory);
        });

        // Register the message engine factory
        // Note: Kafka uses IConsumer<byte[], byte[]> from Confluent.Kafka as the client type
        services.AddSingleton<IMessageEngineFactory<TMessage?, IKafkaConsumerClientWrapper>, KafkaMessageEngineFactory<TMessage>>();

        // Register the hosted service that manages consumer workers
        services.AddConsumerHostedService<TMessage?, IKafkaConsumerClientWrapper>(consumerName);
    }
}