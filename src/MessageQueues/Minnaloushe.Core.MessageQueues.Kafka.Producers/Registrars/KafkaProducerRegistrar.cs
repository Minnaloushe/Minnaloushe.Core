using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.ClientProviders.Kafka;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.Abstractions.Routines;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Extensions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Registrars;
using System.Text.Json;

namespace Minnaloushe.Core.MessageQueues.Kafka.Producers.Registrars;

/// <summary>
/// Type-safe producer registrar for Kafka producers.
/// </summary>
internal class KafkaProducerRegistrar<TMessage> : IProducerRegistrar where TMessage : class
{
    public void Register(IServiceCollection services, string producerName, string connectionName, object? producerOptions)
    {
        var typedOptions = producerOptions as ProducerOptions<TMessage> ?? new ProducerOptions<TMessage>();

        services.AddKeyedSingleton<IKafkaAdminClientProvider>(producerName,
            (sp, _) => sp.GetRequiredKeyedService<IKafkaAdminClientProvider>(connectionName));

        // Register keyed client provider alias from producer name -> connection name
        services.AddKeyedSingleton<IKafkaProducerClientProvider>(producerName,
            (sp, _) => sp.GetRequiredKeyedService<IKafkaProducerClientProvider>(connectionName));

        // Register the producer (keyed by producer name)
        // Use connectionName for options lookup since that's where Parameters are registered
        services.AddKeyedSingleton<KafkaProducer<TMessage>>(producerName, (sp, key) =>
        {
            var clientProvider = sp.GetRequiredKeyedService<IKafkaProducerClientProvider>(key);
            var routines = sp.GetRequiredService<IMessageQueueNamingConventionsProvider>();
            var adminProvider = sp.GetRequiredKeyedService<IKafkaAdminClientProvider>(connectionName);
            var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<MessageQueueOptions>>();
            var logger = sp.GetRequiredService<ILogger<KafkaProducer<TMessage>>>();
            var jsonOptions = sp.GetRequiredService<JsonSerializerOptions>();
            return new KafkaProducer<TMessage>(clientProvider, routines, adminProvider, optionsMonitor, jsonOptions, logger, connectionName, typedOptions);
        });

        // Register the IProducer interface (keyed by producer name)
        services.AddKeyedSingleton<IProducer<TMessage>>(producerName,
            (sp, key) => sp.GetRequiredKeyedService<KafkaProducer<TMessage>>(key));

        // Also register by connection name for resolution by connectionName
        services.AddKeyedSingleton<IProducer<TMessage>>(connectionName,
            (sp, key) => sp.GetRequiredKeyedService<IProducer<TMessage>>(producerName));

        // Register the IProducer interface (non-keyed) for convenience when only one producer per message type exists
        services.AddSingleton<IProducer<TMessage>>(
            sp => sp.GetRequiredKeyedService<IProducer<TMessage>>(producerName));
    }
}