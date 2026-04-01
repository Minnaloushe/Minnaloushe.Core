using Microsoft.Extensions.Logging;

namespace Minnaloushe.Core.MessageQueues.Kafka.Consumers;

internal static partial class KafkaDeadLetterPublisherLogger
{
    [LoggerMessage(
        EventId = 6000,
        Level = LogLevel.Warning,
        Message = "Failed to publish to dead letter topic '{DeadLetterTopic}' (attempt {Attempt}/{Retries}), trying to ensure topic exists...")]
    public static partial void LogFailedToPublishToDeadLetter(this ILogger logger, string deadLetterTopic, int attempt, int retries, Exception exception);

    [LoggerMessage(
        EventId = 6001,
        Level = LogLevel.Error,
        Message = "Failed to ensure dead letter topic '{DeadLetterTopic}' exists before retrying")]
    public static partial void LogFailedToEnsureTopicExistsBeforeRetry(this ILogger logger, string deadLetterTopic, Exception exception);

    [LoggerMessage(
        EventId = 6002,
        Level = LogLevel.Warning,
        Message = "Message sent to dead letter topic '{DeadLetterTopic}' from original topic '{OriginalTopic}'")]
    public static partial void LogMessageSentToDeadLetter(this ILogger logger, string deadLetterTopic, string originalTopic);

    [LoggerMessage(
        EventId = 6003,
        Level = LogLevel.Information,
        Message = "Dead letter topic '{TopicName}' created on demand")]
    public static partial void LogDeadLetterTopicCreated(this ILogger logger, string topicName);

    [LoggerMessage(
        EventId = 6004,
        Level = LogLevel.Debug,
        Message = "Dead letter topic '{TopicName}' already exists (created by another process)")]
    public static partial void LogDeadLetterTopicAlreadyExists(this ILogger logger, string topicName);

    [LoggerMessage(
        EventId = 6005,
        Level = LogLevel.Error,
        Message = "Failed to create dead letter topic '{TopicName}'")]
    public static partial void LogFailedToCreateDeadLetterTopic(this ILogger logger, string topicName, Exception exception);
}
