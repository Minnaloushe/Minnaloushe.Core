using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.Abstractions.ErrorHandling;
using Minnaloushe.Core.MessageQueues.Abstractions.Routines;
using RabbitMQ.Client;

namespace Minnaloushe.Core.MessageQueues.RabbitMq.Consumers;

/// <summary>
/// Factory for creating RabbitMQ message engines.
/// </summary>
public class RabbitMqMessageEngineFactory<TMessage>(
    IServiceProvider serviceProvider
) : IMessageEngineFactory<TMessage, IConnection>
{
    public IMessageEngine CreateEngine(
        string name,
        IConnection provider,
        IConsumer<TMessage> consumer,
        MessageQueueOptions options,
        IErrorHandlingStrategy errorHandlingStrategy)
    {
        var routines = serviceProvider.GetRequiredService<IMessageQueueNamingConventionsProvider>();

        var logger = serviceProvider.GetRequiredService<ILogger<RabbitMqMessageEngine<TMessage>>>();

        return new RabbitMqMessageEngine<TMessage>(
            name,
            provider,
            consumer,
            options,
            errorHandlingStrategy,
            routines,
            logger);
    }
}