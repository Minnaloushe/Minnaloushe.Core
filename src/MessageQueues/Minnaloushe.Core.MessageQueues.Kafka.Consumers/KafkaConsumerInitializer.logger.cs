using Microsoft.Extensions.Logging;

namespace Minnaloushe.Core.MessageQueues.Kafka.Consumers;

internal static partial class KafkaConsumerInitializerLogger
{
    [LoggerMessage(
        EventId = 7000,
        Level = LogLevel.Information,
        Message = "Expanded partitions for topic '{TopicName}' from {CurrentPartitions} to {DesiredPartitions}")]
    public static partial void LogTopicPartitionsExpanded(this ILogger logger, string topicName, int currentPartitions, int desiredPartitions);

    [LoggerMessage(
        EventId = 7001,
        Level = LogLevel.Warning,
        Message = "Topic '{TopicName}' has {CurrentPartitions} partitions but desired is {DesiredPartitions}. Reducing partitions is not supported by Kafka, skipping")]
    public static partial void LogTopicPartitionReductionSkipped(this ILogger logger, string topicName, int currentPartitions, int desiredPartitions);

    [LoggerMessage(
        EventId = 7002,
        Level = LogLevel.Information,
        Message = "Updated configuration for topic '{TopicName}'")]
    public static partial void LogTopicConfigurationUpdated(this ILogger logger, string topicName);

    [LoggerMessage(
        EventId = 7003,
        Level = LogLevel.Information,
        Message = "Topic '{TopicName}' created")]
    public static partial void LogTopicCreated(this ILogger logger, string topicName);

    [LoggerMessage(
        EventId = 7004,
        Level = LogLevel.Debug,
        Message = "Topic '{TopicName}' already exists")]
    public static partial void LogTopicAlreadyExists(this ILogger logger, string topicName);

    [LoggerMessage(
        EventId = 7005,
        Level = LogLevel.Error,
        Message = "Failed to create topic '{TopicName}'")]
    public static partial void LogTopicCreationFailed(this ILogger logger, Exception exception, string topicName);

    [LoggerMessage(
        EventId = 7006,
        Level = LogLevel.Debug,
        Message = "Starting consumer initializer for '{ConsumerName}' (message type: '{MessageType}')")]
    public static partial void LogConsumerInitializerStarting(this ILogger logger, string consumerName, string messageType);

    [LoggerMessage(
        EventId = 7007,
        Level = LogLevel.Debug,
        Message = "Resolved topic name '{TopicName}' for consumer '{ConsumerName}'")]
    public static partial void LogConsumerTopicResolved(this ILogger logger, string consumerName, string topicName);

    [LoggerMessage(
        EventId = 7008,
        Level = LogLevel.Debug,
        Message = "Resolved options for consumer '{ConsumerName}': connection='{ConnectionName}', type='{ConnectionType}'")]
    public static partial void LogConsumerOptionsResolved(this ILogger logger, string consumerName, string connectionName, string connectionType);
}
