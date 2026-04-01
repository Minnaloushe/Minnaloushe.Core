namespace Minnaloushe.Core.MessageQueues.Abstractions;

//TODO: Key parameter needs to be reconsidered. May be should introduce PublishOptions and put key and headers there.
// Ideally introduce optional routine that will be registered with producer and will extract key from message.
public interface IProducer<in TMessage>
{
    /// <summary>
    /// Publishes a message to the message queue.
    /// </summary>
    /// <param name="message">The message to publish.</param>
    /// <param name="key">Optional key for partitioning the message (for now used only by kafka).</param>
    /// <param name="headers">Optional headers to include with the message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync(TMessage? message, string? key = null, IReadOnlyDictionary<string, string>? headers = null, CancellationToken cancellationToken = default);
}