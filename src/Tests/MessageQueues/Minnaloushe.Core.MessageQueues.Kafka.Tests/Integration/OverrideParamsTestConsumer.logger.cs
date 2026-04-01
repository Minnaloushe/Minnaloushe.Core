using Microsoft.Extensions.Logging;

namespace Minnaloushe.Core.MessageQueues.Kafka.Tests.Integration;

internal static partial class OverrideParamsTestConsumerLogger
{
    [LoggerMessage(EventId = 9000, Level = LogLevel.Information, Message = "Received message: {Data}")]
    public static partial void ReceivedMessage(this ILogger<KafkaConsumerTopicInitializationWithOverrideTests.OverrideParamsTestConsumer> logger, string Data);
}
