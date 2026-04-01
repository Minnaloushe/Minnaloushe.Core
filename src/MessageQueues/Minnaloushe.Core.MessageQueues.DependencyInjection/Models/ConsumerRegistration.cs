namespace Minnaloushe.Core.MessageQueues.DependencyInjection.Models;

/// <summary>
/// Describes a consumer registration with its message type and optional custom name.
/// </summary>
public sealed record ConsumerRegistration
{
    /// <summary>
    /// The type of message the consumer handles.
    /// </summary>
    public required Type MessageType { get; init; }

    /// <summary>
    /// The custom name for the consumer. If null, will use MqNaming.GetSafeName&lt;TMessage&gt;().
    /// This name is used to match configuration from MessageQueues:Consumers:Name.
    /// </summary>
    public string? Name { get; init; }
    public int Parallelism { get; init; }
}
