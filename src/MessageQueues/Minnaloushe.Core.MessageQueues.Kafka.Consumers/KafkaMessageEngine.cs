using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Minnaloushe.Core.ClientProviders.Kafka.Wrappers;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.Abstractions.ErrorHandling;
using Minnaloushe.Core.MessageQueues.Abstractions.Routines;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace Minnaloushe.Core.MessageQueues.Kafka.Consumers;

//TODO Refactor this. It is a workaround to provide a choice between fast consumer and reliable consumer for long-running operations
internal class ReliableKafkaMessageEngine<TMessage>(
    string consumerName,
    IKafkaConsumerClientWrapper provider,
    IConsumer<TMessage?> consumer,
    IErrorHandlingStrategy errorHandlingStrategy,
    IMessageQueueNamingConventionsProvider namingConventionsProvider,
    MessageQueueOptions options,
    JsonSerializerOptions jsonOptions,
    ILogger<ReliableKafkaMessageEngine<TMessage?>> logger
)
    : ConsumerEngine<TMessage?>(consumerName, consumer, errorHandlingStrategy, namingConventionsProvider, options, logger)
{
    //TODO: Move to configuration
    private readonly TimeSpan _keepAliveInterval = TimeSpan.FromMilliseconds(200);

    private readonly string _topicName = namingConventionsProvider.GetTopicName<TMessage>();
    private readonly Channel<ConsumeResult<byte[], byte[]>> _messageChannel =
        Channel.CreateBounded<ConsumeResult<byte[], byte[]>>(1);
    private volatile bool _pendingResume;
    private bool _handlerCompleted;
    private ConsumeResult<byte[], byte[]>? _pendingCommit;
    private Task _consumeLoopTask = Task.CompletedTask;
    private CancellationTokenSource? _loopCts;

    protected override async Task<IMessageContext<TMessage?>> ReceiveAsync(CancellationToken ct)
    {
        if (_handlerCompleted)
        {
            _pendingResume = true;
        }

        var result = await _messageChannel.Reader.ReadAsync(ct);
        _handlerCompleted = true;

        var key = result.Message.Key is null || result.Message.Key.Length <= 0
            ? null
            : Encoding.UTF8.GetString(result.Message.Key);

        var rawBytes = result.Message.Value ?? [];
        var message = rawBytes is { Length: > 0 }
            ? JsonSerializer.Deserialize<TMessage>(rawBytes, jsonOptions)
            : default;

        var headers = ExtractHeaders(result.Message.Headers);
        IMessageContext<TMessage?> context = new KafkaMessageContext<TMessage?>(key, r => _pendingCommit = r, result, message, rawBytes, headers);
        return context;
    }

    //Need this workaround to avoid the consumer being evicted by the broker
    //while the handler is running.
    private void PollingLoop(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Blocks efficiently until a message arrives or cancellation is requested
                try
                {
                    var result = provider.Consumer.Consume(ct);

                    if (result is null)
                    {
                        continue;
                    }

                    var partitions = provider.Consumer.Assignment;
                    if (partitions.Count > 0)
                    {
                        provider.Consumer.Pause(partitions);
                    }

                    _messageChannel.Writer.TryWrite(result);

                    // Partitions are paused: Consume returns null immediately each tick.
                    // This keeps rd_kafka_consumer_poll() firing regularly so the broker
                    // does not evict the consumer while the handler runs.
                    while (!_pendingResume && !ct.IsCancellationRequested)
                    {
                        provider.Consumer.Consume(_keepAliveInterval);
                    }

                    if (ct.IsCancellationRequested)
                    {
                        break;
                    }

                    _pendingResume = false;

                    // Commit is performed here on the polling thread to avoid concurrent
                    // access on the consumer instance (Confluent.Kafka is not thread-safe).
                    // _pendingCommit is written by the handler thread before the volatile
                    // write of _pendingResume, so it is visible here after the volatile read.
                    var commitResult = _pendingCommit;
                    if (commitResult is not null)
                    {
                        provider.Consumer.Commit(commitResult);
                        _pendingCommit = null;
                    }

                    var resumePartitions = provider.Consumer.Assignment;
                    if (resumePartitions.Count > 0)
                    {
                        provider.Consumer.Resume(resumePartitions);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning("Error during consumer {ConsumerName} polling loop. {Message}", ConsumerName, ex.Message);
                }
            }
        }
        catch (OperationCanceledException ex)
        {
            logger.LogInformation(ex, "Polling loop cancelled for consumer '{ConsumerName}'", ConsumerName);
        }
    }

    private static IReadOnlyDictionary<string, string>? ExtractHeaders(Headers? headers)
    {
        if (headers is null or { Count: 0 })
        {
            return null;
        }

        var result = new Dictionary<string, string>();
        foreach (var header in headers)
        {
            result[header.Key] = Encoding.UTF8.GetString(header.GetValueBytes());
        }

        return result;
    }

    public override async Task StopAsync(CancellationToken ct)
    {
        _loopCts?.Cancel();
        try
        {
            await _consumeLoopTask;
        }
        finally
        {
            provider.Consumer.Close();
        }
    }

    public override Task OnStartAsync(CancellationToken ct)
    {
        provider.Consumer.Subscribe(_topicName);
        logger.LogInformation("Kafka consumer subscribed to topic: {TopicName}", _topicName);
        _loopCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _consumeLoopTask = Task.Run(() => PollingLoop(_loopCts.Token), _loopCts.Token);
        return Task.CompletedTask;
    }
}

internal class FastKafkaMessageEngine<TMessage>(
    string consumerName,
    IKafkaConsumerClientWrapper provider,
    IConsumer<TMessage?> consumer,
    IErrorHandlingStrategy errorHandlingStrategy,
    IMessageQueueNamingConventionsProvider namingConventionsProvider,
    MessageQueueOptions options,
    JsonSerializerOptions jsonOptions,
    ILogger<FastKafkaMessageEngine<TMessage?>> logger
)
    : ConsumerEngine<TMessage?>(consumerName, consumer, errorHandlingStrategy, namingConventionsProvider, options, logger)
{
    private readonly string _topicName = namingConventionsProvider.GetTopicName<TMessage>();

    protected override Task<IMessageContext<TMessage?>> ReceiveAsync(CancellationToken ct)
    {
        var result = provider.Consumer.Consume(ct);

        var key = result.Message.Key is null || result.Message.Key.Length <= 0
            ? null
            : Encoding.UTF8.GetString(result.Message.Key);

        var rawBytes = result.Message.Value ?? [];
        var message = rawBytes is { Length: > 0 }
            ? JsonSerializer.Deserialize<TMessage>(rawBytes, jsonOptions)
            : default;

        var headers = ExtractHeaders(result.Message.Headers);
        IMessageContext<TMessage?> context = new KafkaMessageContext<TMessage?>(key, r => provider.Consumer.Commit(r), result, message, rawBytes, headers);
        return Task.FromResult(context);
    }

    private static IReadOnlyDictionary<string, string>? ExtractHeaders(Headers? headers)
    {
        if (headers is null or { Count: 0 })
        {
            return null;
        }

        var result = new Dictionary<string, string>();
        foreach (var header in headers)
        {
            result[header.Key] = Encoding.UTF8.GetString(header.GetValueBytes());
        }

        return result;
    }

    public override Task StopAsync(CancellationToken ct)
    {
        provider.Consumer.Close();
        return Task.CompletedTask;
    }

    public override Task OnStartAsync(CancellationToken ct)
    {
        // Subscribe to the topic before consuming messages
        provider.Consumer.Subscribe(_topicName);
        logger.LogInformation("Kafka consumer subscribed to topic: {TopicName}", _topicName);
        return Task.CompletedTask;
    }
}