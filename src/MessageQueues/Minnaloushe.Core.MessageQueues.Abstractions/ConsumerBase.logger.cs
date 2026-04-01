using Microsoft.Extensions.Logging;

namespace Minnaloushe.Core.MessageQueues.Abstractions.Consumers;

public static partial class ConsumerBaseLogger
{
    [LoggerMessage(
        EventId = 5000,
        Level = LogLevel.Information,
        Message = "Consumer started")]
    public static partial void LogConsumerStarted(this ILogger logger);

    [LoggerMessage(
        EventId = 5001,
        Level = LogLevel.Warning,
        Message = "Consumer has already been started")]
    public static partial void LogConsumerAlreadyStarted(this ILogger logger);

    [LoggerMessage(
        EventId = 5002,
        Level = LogLevel.Information,
        Message = "Consumer stopping requested")]
    public static partial void LogConsumerStoppingRequested(this ILogger logger);

    [LoggerMessage(
        EventId = 5003,
        Level = LogLevel.Information,
        Message = "Consumer stopped")]
    public static partial void LogConsumerStopped(this ILogger logger);

    [LoggerMessage(
        EventId = 5004,
        Level = LogLevel.Information,
        Message = "Consumer stopped by cancellation")]
    public static partial void LogConsumerStoppedByCancellation(this ILogger logger);

    [LoggerMessage(
        EventId = 5005,
        Level = LogLevel.Debug,
        Message = "Consumer loop started")]
    public static partial void LogConsumerLoopStarted(this ILogger logger);

    [LoggerMessage(
        EventId = 5006,
        Level = LogLevel.Debug,
        Message = "Consumer loop completed")]
    public static partial void LogConsumerLoopCompleted(this ILogger logger);

    [LoggerMessage(
        EventId = 5007,
        Level = LogLevel.Debug,
        Message = "Consumer loop cancelled")]
    public static partial void LogConsumerLoopCancelled(this ILogger logger);

    [LoggerMessage(
        EventId = 5008,
        Level = LogLevel.Error,
        Message = "Error in consumer loop, will retry after delay")]
    public static partial void LogConsumerLoopError(this ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 5009,
        Level = LogLevel.Trace,
        Message = "Client acquired from provider")]
    public static partial void LogClientAcquired(this ILogger logger);

    [LoggerMessage(
        EventId = 5010,
        Level = LogLevel.Trace,
        Message = "Client lease released")]
    public static partial void LogClientReleased(this ILogger logger);

    [LoggerMessage(
        EventId = 5011,
        Level = LogLevel.Debug,
        Message = "Message received from queue")]
    public static partial void LogMessageReceived(this ILogger logger);

    [LoggerMessage(
        EventId = 5012,
        Level = LogLevel.Debug,
        Message = "Message processed successfully")]
    public static partial void LogMessageProcessed(this ILogger logger);

    [LoggerMessage(
        EventId = 5013,
        Level = LogLevel.Debug,
        Message = "Message processing cancelled")]
    public static partial void LogMessageProcessingCancelled(this ILogger logger);

    [LoggerMessage(
        EventId = 5014,
        Level = LogLevel.Error,
        Message = "Error processing message, will continue with next message")]
    public static partial void LogMessageProcessingError(this ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 5015,
        Level = LogLevel.Debug,
        Message = "Client was disposed, will acquire new client")]
    public static partial void LogClientDisposed(this ILogger logger);

    [LoggerMessage(
        EventId = 5016,
        Level = LogLevel.Debug,
        Message = "Client lease timeout expired, will acquire new client")]
    public static partial void LogClientLeaseTimeout(this ILogger logger);

    [LoggerMessage(
        EventId = 5017,
        Level = LogLevel.Error,
        Message = "Failed to reject message after processing error")]
    public static partial void LogRejectFailed(this ILogger logger, Exception ex);
    [LoggerMessage(
        EventId = 5018,
        Level = LogLevel.Information,
        Message = "Client epoch changed (old: {CurrentEpoch}, new: {NewEpoch}). Subscription will be reinitialized")]
    public static partial void LogClientEpochChanged(this ILogger logger, long currentEpoch, long newEpoch);
    [LoggerMessage(
        EventId = 5019,
        Level = LogLevel.Warning,
        Message = "Message with empty body si received. Rejecting...")]
    public static partial void LogMessageRejectedDueToEmptyBody(this ILogger logger);

}
