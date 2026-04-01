using Microsoft.Extensions.Logging;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.Abstractions.ErrorHandling;
using Minnaloushe.Core.MessageQueues.Abstractions.Routines;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Threading.Channels;

namespace Minnaloushe.Core.MessageQueues.RabbitMq.Consumers;

internal class RabbitMqMessageEngine<TMessage>(
    string consumerName,
    IConnection provider,
    IConsumer<TMessage> consumer,
    MessageQueueOptions options,
    IErrorHandlingStrategy errorHandlingStrategy,
    IMessageQueueNamingConventionsProvider namingConventionsProvider,
    ILogger<RabbitMqMessageEngine<TMessage>> logger
) : ConsumerEngine<TMessage>(consumerName, consumer, errorHandlingStrategy, namingConventionsProvider, options: options, logger)
{
    private IChannel? _channel;
    private AsyncEventingBasicConsumer? _eventConsumer;

    private readonly Channel<IMessageContext<TMessage>> _buffer =
        Channel.CreateBounded<IMessageContext<TMessage>>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

    private string _consumerTag = string.Empty;
    private readonly IMessageQueueNamingConventionsProvider _namingConventionsProvider = namingConventionsProvider;

    protected override async Task<IMessageContext<TMessage>> ReceiveAsync(CancellationToken ct)
    {
        var result = await _buffer.Reader.ReadAsync(ct);
        return result;
    }

    public override async Task StopAsync(CancellationToken ct)
    {
        if (_channel != null)
        {
            await _channel.BasicCancelAsync(_consumerTag, cancellationToken: ct);

            await WaitForIdleAsync();

            _buffer.Writer.Complete();
            await _buffer.Reader.Completion;
            await _channel.CloseAsync(ct);
            await _channel.DisposeAsync();

            _channel = null;
        }
        else
        {
            logger.LogWarning("Trying to stop engine that was not started");
        }
    }

    public override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        _channel = await provider.CreateChannelAsync(cancellationToken: cancellationToken);
        _eventConsumer = new AsyncEventingBasicConsumer(_channel);

        _eventConsumer.ReceivedAsync += async (_, ea) =>
        {
            var ctx = new RabbitMessageContext<TMessage>(_channel, ea);
            await _buffer.Writer.WriteAsync(ctx, cancellationToken);
        };
        var queueName = _namingConventionsProvider.GetServiceKey<TMessage>(Options);
        _consumerTag = await _channel.BasicConsumeAsync(queueName, false, _eventConsumer, cancellationToken: cancellationToken);
    }
}