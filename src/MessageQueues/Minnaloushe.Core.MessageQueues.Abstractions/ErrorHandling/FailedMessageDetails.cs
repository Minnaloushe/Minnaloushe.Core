namespace Minnaloushe.Core.MessageQueues.Abstractions.ErrorHandling;

/// <summary>
/// Represents details about a failed message for error handling.
/// </summary>
/// <param name="OriginalMessage">The original message bytes.</param>
/// <param name="Exception">The exception that caused the failure.</param>
/// <param name="Topic">The original topic or exchange name.</param>
/// <param name="Timestamp">When the failure occurred.</param>
/// <param name="MessageType">The type of the original message.</param>
public record FailedMessageDetails(
    ReadOnlyMemory<byte> OriginalMessage,
    Exception? Exception,
    string Topic,
    string ServiceKey,
    DateTimeOffset Timestamp,
    Type MessageType)
{
    /// <summary>
    /// Optional additional headers/properties from the original message.
    /// </summary>
    public IReadOnlyDictionary<string, string>? OriginalHeaders { get; init; }
}