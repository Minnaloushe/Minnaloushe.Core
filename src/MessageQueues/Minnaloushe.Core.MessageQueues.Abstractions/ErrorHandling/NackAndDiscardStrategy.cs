using Microsoft.Extensions.Logging;

namespace Minnaloushe.Core.MessageQueues.Abstractions.ErrorHandling;

/// <summary>
/// Strategy that discards failed messages without retrying.
/// </summary>
internal sealed partial class NackAndDiscardStrategy(ILogger logger) : IErrorHandlingStrategy
{
    public Task<ErrorHandlingResult> HandleErrorAsync(FailedMessageDetails details, CancellationToken cancellationToken)
    {
        LogMessageDiscarded(details.Topic, details.Exception?.Message ?? "Unknown error");
        return Task.FromResult(ErrorHandlingResult.Discarded);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Message from '{TopicOrQueue}' discarded. Error: {ErrorMessage}")]
    private partial void LogMessageDiscarded(string topicOrQueue, string errorMessage);
}