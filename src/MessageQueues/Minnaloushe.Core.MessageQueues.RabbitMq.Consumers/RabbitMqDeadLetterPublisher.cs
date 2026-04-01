using Microsoft.Extensions.Logging;
using Minnaloushe.Core.ClientProviders.Abstractions;
using Minnaloushe.Core.MessageQueues.Abstractions.ErrorHandling;
using RabbitMQ.Client;
using System.Text;

namespace Minnaloushe.Core.MessageQueues.RabbitMq.Consumers;

/// <summary>
/// RabbitMQ implementation of dead letter publisher.
/// Creates DLT queues on demand if they don't exist.
/// </summary>
public sealed class RabbitMqDeadLetterPublisher(
    IClientProvider<IConnection> clientProvider,
    ILogger<RabbitMqDeadLetterPublisher> logger
) : IDeadLetterPublisher
{
    private readonly SemaphoreSlim _channelLock = new(1, 1);
    private IChannel? _channel;
    private long _currentEpoch;
    private readonly HashSet<string> _declaredQueues = [];

    public async Task PublishToDeadLetterAsync(
        string deadLetterDestination,
        FailedMessageDetails details,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken)
    {
        using var lease = clientProvider.Acquire();
        var channel = await GetOrCreateChannelAsync(lease, cancellationToken);

        // Ensure DLT queue exists (only on first publish to this queue)
        if (!_declaredQueues.Contains(deadLetterDestination))
        {
            try
            {
                // QueueDeclareAsync is idempotent - safe to call even if queue exists
                await channel.QueueDeclareAsync(
                    queue: deadLetterDestination,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null,
                    cancellationToken: cancellationToken);

                _declaredQueues.Add(deadLetterDestination);
                logger.LogDeadLetterQueueCreated(deadLetterDestination);
            }
            catch (Exception ex)
            {
                logger.LogFailedToCreateDeadLetterQueue(ex, deadLetterDestination);
                throw;
            }
        }

        // Create basic properties with error details
        var properties = new BasicProperties
        {
            Persistent = true,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            Headers = new Dictionary<string, object?>()
        };

        // Add all error headers to properties
        foreach (var (key, value) in headers)
        {
            properties.Headers[key] = Encoding.UTF8.GetBytes(value);
        }

        // Publish directly to the DLT queue (default exchange with queue name as routing key)
        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: deadLetterDestination,
            mandatory: false,
            basicProperties: properties,
            body: details.OriginalMessage,
            cancellationToken: cancellationToken);

        logger.LogMessageSentToDeadLetter(deadLetterDestination, details.Topic);
    }

    private async Task<IChannel> GetOrCreateChannelAsync(ClientProviders.Abstractions.ClientLease.IClientLease<IConnection> lease, CancellationToken ct)
    {
        if (_channel != null && _currentEpoch == lease.Epoch)
        {
            return _channel;
        }

        await _channelLock.WaitAsync(ct);
        try
        {
            if (_channel != null && _currentEpoch == lease.Epoch)
            {
                return _channel;
            }

            if (_channel != null)
            {
                await _channel.DisposeAsync();
            }

            _channel = await lease.Client.CreateChannelAsync(cancellationToken: ct);
            _currentEpoch = lease.Epoch;
            _declaredQueues.Clear(); // Clear cache when channel is recreated

            return _channel;
        }
        finally
        {
            _channelLock.Release();
        }
    }
}
