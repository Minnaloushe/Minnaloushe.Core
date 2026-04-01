using Minnaloushe.Core.MessageQueues.Abstractions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace Minnaloushe.Core.MessageQueues.RabbitMq.Consumers;

internal sealed class RabbitMessageContext<T>(IChannel channel, BasicDeliverEventArgs ea)
    : IMessageContext<T>
{
    private readonly ulong _deliveryTag = ea.DeliveryTag;
    private readonly byte[] _rawBytes = ea.Body.ToArray();

    //It is a tradeoff with kafka where. RabbitMQ does not have a concept of message key, so we return null here.
    public string? Key => null;

    public T Message { get; } = JsonSerializer.Deserialize<T>(Encoding.UTF8.GetString(ea.Body.ToArray()))
                                ?? throw new NullReferenceException("Message deserialization returned null");

    public ReadOnlyMemory<byte> RawMessage => _rawBytes;

    public IReadOnlyDictionary<string, string>? Headers { get; } = ExtractHeaders(ea.BasicProperties);

    public async Task AckAsync(CancellationToken serviceStop)
    {
        await channel.BasicAckAsync(_deliveryTag, multiple: false, cancellationToken: serviceStop);
    }

    public async Task NackAsync(bool requeue, CancellationToken serviceStop)
    {
        await channel.BasicNackAsync(_deliveryTag, multiple: false, requeue, cancellationToken: serviceStop);
    }

    private static IReadOnlyDictionary<string, string>? ExtractHeaders(IReadOnlyBasicProperties? properties)
    {
        if (properties?.Headers is null or { Count: 0 })
        {
            return null;
        }

        var result = new Dictionary<string, string>();
        foreach (var (key, value) in properties.Headers)
        {
            if (value is byte[] bytes)
            {
                result[key] = Encoding.UTF8.GetString(bytes);
            }
            else if (value is not null)
            {
                result[key] = value.ToString() ?? string.Empty;
            }
        }

        return result;
    }
}