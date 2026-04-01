using Microsoft.Extensions.Logging;

namespace Minnaloushe.Core.MessageQueues.Abstractions.Consumers;

internal static partial class ConsumerHostedServiceLoggerMessages
{
    [LoggerMessage(
        EventId = 6000,
        Level = LogLevel.Information,
        Message = "Starting consumer hosted service for '{ConsumerName}'")]
    public static partial void LogConsumerHostedServiceStarting(this ILogger logger, string consumerName);

    [LoggerMessage(
        EventId = 6001,
        Level = LogLevel.Information,
        Message = "Consumer hosted service started for '{ConsumerName}' with {WorkerCount} workers")]
    public static partial void LogConsumerHostedServiceStarted(this ILogger logger, string consumerName, int workerCount);

    [LoggerMessage(
        EventId = 6002,
        Level = LogLevel.Information,
        Message = "Stopping consumer hosted service for '{ConsumerName}'")]
    public static partial void LogConsumerHostedServiceStopping(this ILogger logger, string consumerName);

    [LoggerMessage(
        EventId = 6003,
        Level = LogLevel.Information,
        Message = "Consumer hosted service stopped for '{ConsumerName}'")]
    public static partial void LogConsumerHostedServiceStopped(this ILogger logger, string consumerName);

    [LoggerMessage(
        EventId = 6004,
        Level = LogLevel.Debug,
        Message = "Starting {WorkerCount} consumer workers for '{ConsumerName}'")]
    public static partial void LogStartingConsumerWorkers(this ILogger logger, string consumerName, int workerCount);

    [LoggerMessage(
        EventId = 6005,
        Level = LogLevel.Debug,
        Message = "Running consumer initializer for '{ConsumerName}'")]
    public static partial void LogRunningConsumerInitializer(this ILogger logger, string consumerName);

    [LoggerMessage(
        EventId = 6006,
        Level = LogLevel.Debug,
        Message = "Consumer initializer completed for '{ConsumerName}'")]
    public static partial void LogConsumerInitializerCompleted(this ILogger logger, string consumerName);

    [LoggerMessage(
        EventId = 6007,
        Level = LogLevel.Debug,
        Message = "No consumer initializer found for '{ConsumerName}'")]
    public static partial void LogNoConsumerInitializerFound(this ILogger logger, string consumerName);

    [LoggerMessage(
        EventId = 6008,
        Level = LogLevel.Error,
        Message = "Consumer initializer failed for '{ConsumerName}'")]
    public static partial void LogConsumerInitializerFailed(this ILogger logger, string consumerName, Exception ex);
}
