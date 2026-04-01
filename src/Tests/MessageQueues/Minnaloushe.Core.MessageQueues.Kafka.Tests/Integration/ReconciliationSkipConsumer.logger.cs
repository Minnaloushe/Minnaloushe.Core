using Microsoft.Extensions.Logging;

namespace Minnaloushe.Core.MessageQueues.Kafka.Tests.Integration;

internal static partial class ReconciliationSkipConsumerLogger
{
    [LoggerMessage(EventId = 9000, Level = LogLevel.Information, Message = "Received message: {Data}")]
    public static partial void ReceivedMessage(this ILogger<KafkaTopicReconciliationPartitionReductionSkippedTests.ReconciliationSkipConsumer> logger, string Data);
}
