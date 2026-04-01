namespace Minnaloushe.Core.MessageQueues.Abstractions;

public interface IMessageContext<out TMessage>
{
    /// <summary>
    /// Message key (if applicable, e.g. Kafka message key).
    /// </summary>
    string? Key { get; }

    /// <summary>
    /// The deserialized message.
    /// </summary>
    TMessage Message { get; }

    /// <summary>
    /// The raw message bytes (for dead letter handling).
    /// </summary>
    ReadOnlyMemory<byte> RawMessage { get; }

    /// <summary>
    /// Original headers/properties from the message.
    /// </summary>
    IReadOnlyDictionary<string, string>? Headers { get; }

    /// <summary>
    /// Acknowledges successful message processing.
    /// </summary>
    Task AckAsync(CancellationToken serviceStop);

    /// <summary>
    /// Negatively acknowledges the message (processing failed).
    /// </summary>
    Task NackAsync(bool requeue, CancellationToken serviceStop);
}