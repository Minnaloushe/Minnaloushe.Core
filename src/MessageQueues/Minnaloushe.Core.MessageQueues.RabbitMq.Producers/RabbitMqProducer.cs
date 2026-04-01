using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.ClientProviders.Abstractions;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.Abstractions.Routines;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Extensions;
using Minnaloushe.Core.Toolbox.RetryRoutines;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace Minnaloushe.Core.MessageQueues.RabbitMq.Producers;

internal class RabbitMqProducer<TMessage> : IProducer<TMessage>, IAsyncDisposable where TMessage : class
{
    private static readonly ResiliencePropertyKey<TMessage?> MessagePropertyKey = new("Message");

    private readonly IClientProvider<IConnection> _clientProvider;
    private readonly IMessageQueueNamingConventionsProvider _namingConventionsProvider;
    private readonly ILogger<RabbitMqProducer<TMessage>> _logger;
    private readonly IOptionsMonitor<MessageQueueOptions> _optionsMonitor;
    private readonly string _producerName;
    private readonly ResiliencePipeline _retryPolicy;
    private readonly bool _runtimeExchangeResolution;
    private readonly string _staticExchangeName;
    private readonly HashSet<string> _declaredExchanges = [];
    private readonly Lock _exchangesLock = new();

    private readonly SemaphoreSlim _channelLock = new(1, 1);
    private IChannel? _channel;
    private long _currentEpoch;

    private readonly BasicProperties _reusableProperties = new();

    public RabbitMqProducer(
        IClientProvider<IConnection> clientProvider,
        IMessageQueueNamingConventionsProvider namingConventionsProvider,
        IOptionsMonitor<MessageQueueOptions> optionsMonitor,
        ILogger<RabbitMqProducer<TMessage>> logger,
        string producerName,
        ProducerOptions<TMessage>? producerOptions = null)
    {
        _clientProvider = clientProvider;
        _namingConventionsProvider = namingConventionsProvider;
        _logger = logger;
        _optionsMonitor = optionsMonitor;
        _producerName = producerName;
        _staticExchangeName = namingConventionsProvider.GetTopicName<TMessage>();
        _runtimeExchangeResolution = producerOptions?.ResolveMessageTypeAtRuntime ?? false;

        var options = _optionsMonitor.Get(_producerName);

        _retryPolicy = new ResiliencePipelineBuilder()
            .AddTimeout(options.PublishTimeout)
            .AddRetry(
                new RetryStrategyOptions
                {
                    MaxRetryAttempts = options.RetryPolicy.MaxRetries,
                    DelayGenerator = args =>
                        ValueTask.FromResult<TimeSpan?>(options.RetryPolicy.GetDelay(args.AttemptNumber)),
                    OnRetry = async (args) =>
                    {
                        args.Context.Properties.TryGetValue(MessagePropertyKey, out var message);
                        var exchangeName = GetExchangeName(message);

                        _logger.LogWarning(args.Outcome.Exception,
                            "Failed to publish to exchange '{Exchange}' (attempt {Attempt}/{Retries}), " +
                            "trying to ensure exchange exists...",
                            exchangeName, args.AttemptNumber, options.RetryPolicy.MaxRetries);

                        try
                        {
                            await EnsureExchangeExistsAsync(exchangeName, args.Context.CancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to ensure exchange exists before retrying: {Exchange}", exchangeName);
                        }
                    },
                    ShouldHandle = args
                        => ValueTask.FromResult(args.Outcome.Exception is not null)
                }
            )
            .Build();
    }

    private string GetExchangeName(TMessage? message)
    {
        return
            _runtimeExchangeResolution && message is not null
                ? _namingConventionsProvider.GetTopicName(message.GetType())
                : _staticExchangeName;
    }

    public async Task PublishAsync(
        TMessage? message,
        string? key = null,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default
        )
    {
        if (message == null)
        {
            //TODO: Consider throwing an exception here instead of just logging a warning.
            // Publishing a null message is likely a bug in the calling code, and silently ignoring it might make it harder to detect and fix.
            // Still might be valid for kafka if key is provided, so consider the implications before changing this behavior.
            _logger.LogWarning(
                "Trying to publish null message of type {MessageType}. Operation will be ignored.",
                typeof(TMessage).FullName);
            return;
        }

        var context = ResilienceContextPool.Shared.Get(cancellationToken);
        context.Properties.Set(MessagePropertyKey, message);

        try
        {
            await _retryPolicy.ExecuteAsync(async (ctx) =>
            {
                using var lease = _clientProvider.Acquire();
                //TODO refactor, persist channel and recreate only when connection is rotated
                var channel = await GetOrCreateChannelAsync(lease, ctx.CancellationToken);

                var properties = CreateBasicProperties(headers);

                var msg = _runtimeExchangeResolution
                    ? JsonSerializer.Serialize(message, message.GetType())
                    : JsonSerializer.Serialize(message);
                var messageBody = Encoding.UTF8.GetBytes(msg);

                var exchangeName = GetExchangeName(message);

                await channel.BasicPublishAsync(
                    exchangeName,
                    string.Empty,
                    false,
                    properties,
                    messageBody,
                    cancellationToken: ctx.CancellationToken);
            }, context);
        }
        finally
        {
            ResilienceContextPool.Shared.Return(context);
        }
    }

    private BasicProperties CreateBasicProperties(IReadOnlyDictionary<string, string>? headers)
    {
        if (headers is null or { Count: 0 })
        {
            return _reusableProperties;
        }

        var properties = new BasicProperties
        {
            Headers = new Dictionary<string, object?>()
        };

        foreach (var (key, value) in headers)
        {
            properties.Headers[key] = Encoding.UTF8.GetBytes(value);
        }

        return properties;
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

            return _channel;
        }
        finally
        {
            _channelLock.Release();
        }
    }

    private async Task EnsureExchangeExistsAsync(string exchangeName, CancellationToken cancellationToken)
    {
        lock (_exchangesLock)
        {
            if (_declaredExchanges.Contains(exchangeName))
            {
                return;
            }
        }

        using var lease = _clientProvider.Acquire();
        var channel = await GetOrCreateChannelAsync(lease, cancellationToken);

        try
        {
            await channel.ExchangeDeclareAsync(
                exchange: exchangeName,
                type: ExchangeType.Fanout,
                durable: true,
                autoDelete: false,
                arguments: null,
                noWait: false,
                cancellationToken: cancellationToken);

            lock (_exchangesLock)
            {
                _declaredExchanges.Add(exchangeName);
            }

            _logger.LogInformation("Producer exchange '{ExchangeName}' declared on demand", exchangeName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to declare producer exchange '{ExchangeName}'", exchangeName);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _channelLock.WaitAsync();
        try
        {
            if (_channel is not null)
            {
                await _channel.DisposeAsync();
            }
        }
        finally
        {
            _channelLock.Release();
        }

        _channelLock.Dispose();
    }
}