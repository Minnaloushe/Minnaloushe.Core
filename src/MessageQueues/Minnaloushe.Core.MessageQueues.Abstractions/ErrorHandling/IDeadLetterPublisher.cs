namespace Minnaloushe.Core.MessageQueues.Abstractions.ErrorHandling;

/// <summary>
/// Interface for publishing messages to dead letter queues/topics.
/// Each message queue implementation (RabbitMQ, Kafka) provides its own implementation.
/// </summary>
public interface IDeadLetterPublisher
{
    /// <summary>
    /// Publishes a failed message to the dead letter queue/topic.
    /// Creates the DLT queue/topic if it doesn't exist.
    /// </summary>
    /// <param name="deadLetterDestination">The dead letter queue/topic name.</param>
    /// <param name="details">The failed message details.</param>
    /// <param name="headers">Additional headers to include.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishToDeadLetterAsync(
        string deadLetterDestination,
        FailedMessageDetails details,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken);
}
