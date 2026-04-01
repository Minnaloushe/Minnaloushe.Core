using Microsoft.Extensions.Logging;

namespace Minnaloushe.Core.MessageQueues.Abstractions.ErrorHandling;

/// <summary>
/// Strategy that acknowledges messages even when processing fails.
/// </summary>
internal sealed partial class AckOnErrorStrategy(ILogger logger) : IErrorHandlingStrategy
{
    public Task<ErrorHandlingResult> HandleErrorAsync(FailedMessageDetails details, CancellationToken cancellationToken)
    {
        LogMessageAcknowledged(details.Topic, details.Exception?.Message ?? "Unknown error");
        return Task.FromResult(ErrorHandlingResult.Acknowledged);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Message from '{TopicOrQueue}' acknowledged despite error. Error: {ErrorMessage}")]
    private partial void LogMessageAcknowledged(string topicOrQueue, string errorMessage);
}