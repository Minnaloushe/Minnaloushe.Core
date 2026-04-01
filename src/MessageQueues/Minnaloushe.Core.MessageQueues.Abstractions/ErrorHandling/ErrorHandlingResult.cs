namespace Minnaloushe.Core.MessageQueues.Abstractions.ErrorHandling;

/// <summary>
/// Represents the result of an error handling operation.
/// </summary>
public enum ErrorHandlingResult
{
    /// <summary>Message was requeued for retry.</summary>
    Requeued,

    /// <summary>Message was discarded.</summary>
    Discarded,

    /// <summary>Message was sent to dead letter queue/topic.</summary>
    SentToDeadLetter,

    /// <summary>Message was acknowledged despite failure.</summary>
    Acknowledged
}