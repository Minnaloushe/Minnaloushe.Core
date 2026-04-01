using Microsoft.Extensions.Logging;

namespace Minnaloushe.Core.MessageQueues.RabbitMq.Consumers;

internal static partial class RabbitMqDeadLetterPublisherLogger
{
    [LoggerMessage(
        EventId = 6100,
        Level = LogLevel.Warning,
        Message = "Failed to publish to dead letter queue '{DeadLetterQueue}' (attempt {Attempt}/{Retries}), trying to ensure queue exists...")]
    public static partial void LogFailedToPublishToDeadLetter(this ILogger logger, string deadLetterQueue, int attempt, int retries, Exception exception);

    [LoggerMessage(
        EventId = 6101,
        Level = LogLevel.Error,
        Message = "Failed to create dead letter queue '{DeadLetterQueue}' during retry")]
    public static partial void LogFailedToCreateDeadLetterQueueDuringRetry(this ILogger logger, string deadLetterQueue, Exception exception);

    [LoggerMessage(
        EventId = 6102,
        Level = LogLevel.Warning,
        Message = "Message sent to dead letter queue '{DeadLetterQueue}' from original queue '{OriginalQueue}'")]
    public static partial void LogMessageSentToDeadLetter(this ILogger logger, string deadLetterQueue, string originalQueue);

    [LoggerMessage(
        EventId = 6103,
        Level = LogLevel.Information,
        Message = "Dead letter queue '{QueueName}' created on demand")]
    public static partial void LogDeadLetterQueueCreated(this ILogger logger, string queueName);

    [LoggerMessage(
        EventId = 6104,
        Level = LogLevel.Error,
        Message = "Failed to create dead letter queue '{QueueName}'")]
    public static partial void LogFailedToCreateDeadLetterQueue(this ILogger logger, Exception exception, string queueName);
}
