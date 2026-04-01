namespace Minnaloushe.Core.MessageQueues.DependencyInjection.Models;

/// <summary>
/// Represents a producer registration added via AddProducer.
/// </summary>
public sealed record ProducerRegistration
{
    /// <summary>
    /// The type of message the producer handles.
    /// </summary>
    public required Type MessageType { get; init; }

    /// <summary>
    /// The name of the producer.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The connection name this producer uses.
    /// </summary>
    public required string ConnectionName { get; init; }

    //TODO: Refactor this to typed delegate Func<TMessage, string> or Func<TMessage, byte[]> for Kafka,
    // but it would require making ProducerRegistration generic and complicate the registration process.
    // For now, keep it as object?, type is restored during registration, code consumer is not involved
    // in this process and won't see the untyped delegate.
    /// <summary>
    /// Optional function to extract a key from the message for partitioning (Kafka).
    /// </summary>
    public object? ProducerOptions { get; init; }
}
