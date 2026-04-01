using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.Abstractions.ErrorHandling;
using Minnaloushe.Core.MessageQueues.Abstractions.Routines;

namespace Minnaloushe.Core.MessageQueues.Kafka.Consumers.ErrorHandling;

/// <summary>
/// Kafka-specific error handling strategy factory.
/// Creates dead letter strategies that use Kafka topics.
/// </summary>
internal class KafkaErrorHandlingStrategyFactory<TMessage>(
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
        var topicName = namingConventionsProvider.GetTopicName<TMessage>();
        var deadLetterTopicName = $"{topicName}.{namingConventionsProvider.GetDeadLetterSuffix<TMessage>()}";

        return new DeadLetterStrategy(
            consumerName,
            deadLetterTopicName,
            deadLetterPublisher,
            logger);
    }
}