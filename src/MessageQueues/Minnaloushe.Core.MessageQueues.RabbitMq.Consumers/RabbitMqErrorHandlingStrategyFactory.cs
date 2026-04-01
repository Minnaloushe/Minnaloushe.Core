using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.Abstractions.ErrorHandling;
using Minnaloushe.Core.MessageQueues.Abstractions.Routines;

namespace Minnaloushe.Core.MessageQueues.RabbitMq.Consumers;

/// <summary>
/// RabbitMQ-specific error handling strategy factory.
/// Creates dead letter strategies that use RabbitMQ queues.
/// </summary>
public class RabbitMqErrorHandlingStrategyFactory<TMessage>(
    IOptionsMonitor<MessageQueueOptions> optionsMonitor,
    IMessageQueueNamingConventionsProvider namingConventionsProvider,
    IDeadLetterPublisher deadLetterPublisher,
    ILoggerFactory loggerFactory
) : ErrorHandlingStrategyFactory(optionsMonitor, loggerFactory)
{
    protected override IErrorHandlingStrategy CreateDeadLetterStrategy(
        string consumerName,
        MessageQueueOptions options,
        ILogger logger)
    {
        var queueName = namingConventionsProvider.GetServiceKey<TMessage>(options);
        var deadLetterQueueName = $"{queueName}.{namingConventionsProvider.GetDeadLetterSuffix<TMessage>()}";

        return new DeadLetterStrategy(
            consumerName,
            deadLetterQueueName,
            deadLetterPublisher,
            logger);
    }
}