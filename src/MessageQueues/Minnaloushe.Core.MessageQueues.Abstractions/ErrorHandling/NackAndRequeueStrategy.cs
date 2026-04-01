using Microsoft.Extensions.Logging;

namespace Minnaloushe.Core.MessageQueues.Abstractions.ErrorHandling;

/// <summary>
/// Strategy that re-queues failed messages for retry.
/// </summary>
internal sealed partial class NackAndRequeueStrategy(ILogger logger) : IErrorHandlingStrategy
{
    public Task<ErrorHandlingResult> HandleErrorAsync(FailedMessageDetails details, CancellationToken cancellationToken)
    {
        LogMessageRequeued(details.Topic, details.Exception?.Message ?? "Unknown error");
        return Task.FromResult(ErrorHandlingResult.Requeued);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Message from '{TopicOrQueue}' will be requeued. Error: {ErrorMessage}")]
    private partial void LogMessageRequeued(string topicOrQueue, string errorMessage);
}