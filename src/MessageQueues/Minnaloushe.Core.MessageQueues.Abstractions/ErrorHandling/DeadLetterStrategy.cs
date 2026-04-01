using Microsoft.Extensions.Logging;

namespace Minnaloushe.Core.MessageQueues.Abstractions.ErrorHandling;

/// <summary>
/// Strategy that sends failed messages to a dead letter queue/topic.
/// </summary>
public sealed partial class DeadLetterStrategy(
    string consumerName,
    string deadLetterDestination,
    IDeadLetterPublisher deadLetterPublisher,
    ILogger logger
) : IErrorHandlingStrategy
{
    public async Task<ErrorHandlingResult> HandleErrorAsync(FailedMessageDetails details, CancellationToken cancellationToken)
    {
        try
        {
            var headers = DeadLetterHeaders.CreateHeaders(details, consumerName);

            await deadLetterPublisher.PublishToDeadLetterAsync(
                deadLetterDestination,
                details,
                headers,
                cancellationToken);

            LogMessageSentToDeadLetter(details.Topic, deadLetterDestination);
            return ErrorHandlingResult.SentToDeadLetter;
        }
        catch (Exception ex)
        {
            LogDeadLetterPublishFailed(details.Topic, deadLetterDestination, ex);

            // Fall back to discard if we can't send to DLT
            return ErrorHandlingResult.Discarded;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Message from '{TopicOrQueue}' sent to dead letter destination '{DeadLetterDestination}'")]
    private partial void LogMessageSentToDeadLetter(string topicOrQueue, string deadLetterDestination);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to send message from '{TopicOrQueue}' to dead letter destination '{DeadLetterDestination}'")]
    private partial void LogDeadLetterPublishFailed(string topicOrQueue, string deadLetterDestination, Exception ex);
}