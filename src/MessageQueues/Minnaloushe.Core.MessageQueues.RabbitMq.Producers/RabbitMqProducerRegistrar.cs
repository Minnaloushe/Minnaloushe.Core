using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.ClientProviders.Abstractions;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.Abstractions.Routines;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Extensions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Registrars;
using RabbitMQ.Client;

namespace Minnaloushe.Core.MessageQueues.RabbitMq.Producers;

/// <summary>
/// Type-safe producer registrar for RabbitMQ producers.
/// </summary>
internal class RabbitMqProducerRegistrar<TMessage> : IProducerRegistrar where TMessage : class
{
    public void Register(IServiceCollection services, string producerName, string connectionName, object? producerOptions)
    {
        // Register keyed client provider alias from producer name -> connection name
        services.AddKeyedSingleton<IClientProvider<IConnection>>(producerName,
            (sp, key) => sp.GetRequiredKeyedService<IClientProvider<IConnection>>(connectionName));

        // Register the producer (keyed by producer name)
        services.AddKeyedSingleton<RabbitMqProducer<TMessage>>(producerName, (sp, key) =>
        {
            var clientProvider = sp.GetRequiredKeyedService<IClientProvider<IConnection>>(key);
            var namingConventionsProvider = sp.GetRequiredService<IMessageQueueNamingConventionsProvider>();
            var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<MessageQueueOptions>>();
            var logger = sp.GetRequiredService<ILogger<RabbitMqProducer<TMessage>>>();
            var typedProducerOptions = producerOptions as ProducerOptions<TMessage>;
            return new RabbitMqProducer<TMessage>(
                clientProvider,
                namingConventionsProvider,
                optionsMonitor,
                logger,
                connectionName,
                typedProducerOptions);
        });

        // Register the IProducer interface (keyed by producer name)
        services.AddKeyedSingleton<IProducer<TMessage>>(producerName,
            (sp, key) => sp.GetRequiredKeyedService<RabbitMqProducer<TMessage>>(key));

        // Also register by connection name for resolution by connectionName
        services.AddKeyedSingleton<IProducer<TMessage>>(connectionName,
            (sp, key) => sp.GetRequiredKeyedService<IProducer<TMessage>>(producerName));

        // Register the IProducer interface (non-keyed) for convenience when only one producer per message type exists
        services.AddSingleton<IProducer<TMessage>>(
            sp => sp.GetRequiredKeyedService<IProducer<TMessage>>(producerName));
    }
}