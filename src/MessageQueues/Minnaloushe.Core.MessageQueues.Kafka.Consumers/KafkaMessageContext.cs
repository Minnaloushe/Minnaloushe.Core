using Confluent.Kafka;
using Minnaloushe.Core.MessageQueues.Abstractions;

namespace Minnaloushe.Core.MessageQueues.Kafka.Consumers;

internal class KafkaMessageContext<T>
(
    string? key,
    Action<ConsumeResult<byte[], byte[]>> commitAction,
    ConsumeResult<byte[], byte[]> result,
    T? message,
    ReadOnlyMemory<byte> rawMessage,
    IReadOnlyDictionary<string, string>? headers)
    : IMessageContext<T?>
{
    public string? Key { get; } = key;
    public T? Message { get; } = message;

    public ReadOnlyMemory<byte> RawMessage { get; } = rawMessage;

    public IReadOnlyDictionary<string, string>? Headers { get; } = headers;

    public Task AckAsync(CancellationToken cancellationToken)
    {
        commitAction(result);
        return Task.CompletedTask;
    }

    public Task NackAsync(bool requeue, CancellationToken cancellationToken)
    {
        commitAction(result);
        return Task.CompletedTask;
    }
}