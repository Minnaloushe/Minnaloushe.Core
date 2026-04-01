using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Minnaloushe.Core.ClientProviders.Kafka.Options;
using Minnaloushe.Core.ClientProviders.Kafka.Wrappers;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.Abstractions.ErrorHandling;
using Minnaloushe.Core.MessageQueues.Abstractions.Routines;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Kafka.Extensions;
using System.Text.Json;

namespace Minnaloushe.Core.MessageQueues.Kafka.Consumers.Factories;

/// <summary>
/// Factory for creating Kafka message engines.
/// </summary>
internal class KafkaMessageEngineFactory<TMessage>(
    IServiceProvider serviceProvider
) : IMessageEngineFactory<TMessage?, IKafkaConsumerClientWrapper>
{
    public IMessageEngine CreateEngine(
        string consumerName,
        IKafkaConsumerClientWrapper provider,
        IConsumer<TMessage?> consumer,
        MessageQueueOptions options,
        IErrorHandlingStrategy errorHandlingStrategy)
    {
        var routines = serviceProvider.GetRequiredService<IMessageQueueNamingConventionsProvider>();
        var jsonOptions = serviceProvider.GetRequiredService<JsonSerializerOptions>();

        //TODO Rework to factory. Or rework engines
        return options.ToClientOptions().Parameters.EngineType switch
        {
            KafkaEngineType.Reliable =>
                new ReliableKafkaMessageEngine<TMessage>(
                    consumerName,
                    provider,
                    consumer,
                    errorHandlingStrategy,
                    routines,
                    options,
                    jsonOptions,
                    serviceProvider.GetRequiredService<ILogger<ReliableKafkaMessageEngine<TMessage?>>>()),
            KafkaEngineType.Fast =>
                new FastKafkaMessageEngine<TMessage>(
                    consumerName,
                    provider,
                    consumer,
                    errorHandlingStrategy,
                    routines,
                    options,
                    jsonOptions,
                    serviceProvider.GetRequiredService<ILogger<FastKafkaMessageEngine<TMessage?>>>()),

            _ => throw new ArgumentOutOfRangeException(nameof(KafkaParameters.EngineType))
        };
    }
}