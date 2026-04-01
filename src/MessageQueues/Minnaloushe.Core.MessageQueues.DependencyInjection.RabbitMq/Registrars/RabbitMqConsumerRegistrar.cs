using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.ClientProviders.Abstractions;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.Abstractions.ErrorHandling;
using Minnaloushe.Core.MessageQueues.Abstractions.Routines;
using Minnaloushe.Core.MessageQueues.DependencyInjection.RabbitMq.Factories;
using Minnaloushe.Core.MessageQueues.DependencyInjection.RabbitMq.FactorySelectors;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Registrars;
using Minnaloushe.Core.MessageQueues.RabbitMq.Consumers;
using Minnaloushe.Core.Toolbox.AsyncInitializer;
using RabbitMQ.Client;

namespace Minnaloushe.Core.MessageQueues.DependencyInjection.RabbitMq.Registrars;

/// <summary>
/// Type-safe consumer registrar for RabbitMQ consumers.
/// </summary>
internal class RabbitMqConsumerRegistrar<TMessage> : IConsumerRegistrar
{
    public void Register(IServiceCollection services, string consumerName, string connectionName, int parallelism)
    {
        // Register keyed client provider alias from consumer name -> connection name
        // This allows ConsumerHostedService to resolve providers by consumer name
        services.AddKeyedSingleton<IClientProvider<IConnection>>(consumerName,
            (sp, key) => sp.GetRequiredKeyedService<IClientProvider<IConnection>>(connectionName));

        services.AddKeyedSingleton<IClientProvider<IConnection>>(consumerName, (sp, key) =>
        {
            var options = sp.GetRequiredService<IOptionsMonitor<MessageQueueOptions>>().Get(consumerName);
            var factories = sp.GetServices<IRabbitMqClientProviderFactory>();
            var selector = sp.GetRequiredService<IRabbitMqClientProviderFactorySelector>();
            var factory = selector.SelectFactory(options, factories)
                          ?? throw new InvalidOperationException(
                              $"No registered IKafkaConsumerClientProviderFactory can handle consumer '{consumerName}'.");
            return factory.Create(consumerName);

        });
        services.AddSingleton<IAsyncInitializer>(sp =>
        {
            var provider = sp.GetRequiredKeyedService<IClientProvider<IConnection>>(consumerName);
            return provider as IAsyncInitializer
                   ?? throw new InvalidOperationException(
                       $"Consumer provider for '{consumerName}' does not implement IAsyncInitializer.");
        });

        // Register shared client provider for each worker (RabbitMQ can share the same connection)
        for (var i = 0; i < parallelism; i++)
        {
            var workerIndex = i;
            services.AddKeyedSingleton<IClientProvider<IConnection>>($"{consumerName}{workerIndex}",
                (sp, key) => sp.GetRequiredKeyedService<IClientProvider<IConnection>>(consumerName));
        }

        // Register the initializer (keyed by consumer name)
        services.AddKeyedSingleton<IConsumerInitializer>(consumerName, (sp, key) =>
        {
            var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<MessageQueueOptions>>();
            // Use the keyed provider we just registered
            var clientProvider = sp.GetRequiredKeyedService<IClientProvider<IConnection>>(key);
            var routines = sp.GetRequiredService<IMessageQueueNamingConventionsProvider>();
            var logger = sp.GetRequiredService<ILogger<RabbitMqConsumerInitializer<TMessage>>>();

            return new RabbitMqConsumerInitializer<TMessage>(
                consumerName,
                clientProvider,
                optionsMonitor,
                routines,
                logger);
        });

        // Register the dead letter publisher (keyed by consumer name)
        services.AddKeyedSingleton<IDeadLetterPublisher>(consumerName, (sp, key) =>
        {
            var clientProvider = sp.GetRequiredKeyedService<IClientProvider<IConnection>>(key);
            var logger = sp.GetRequiredService<ILogger<RabbitMqDeadLetterPublisher>>();
            return new RabbitMqDeadLetterPublisher(clientProvider, logger);
        });

        // Register the error handling strategy factory (keyed by consumer name)
        services.AddKeyedSingleton<IErrorHandlingStrategyFactory>(consumerName, (sp, key) =>
        {
            var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<MessageQueueOptions>>();
            var routines = sp.GetRequiredService<IMessageQueueNamingConventionsProvider>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var deadLetterPublisher = sp.GetRequiredKeyedService<IDeadLetterPublisher>(key);

            return new RabbitMqErrorHandlingStrategyFactory<TMessage>(
                optionsMonitor,
                routines,
                deadLetterPublisher,
                loggerFactory);
        });

        // Register the message engine factory
        services.AddSingleton<IMessageEngineFactory<TMessage, IConnection>, RabbitMqMessageEngineFactory<TMessage>>();

        // Register the hosted service that manages consumer workers
        services.AddConsumerHostedService<TMessage, IConnection>(consumerName);
    }
}