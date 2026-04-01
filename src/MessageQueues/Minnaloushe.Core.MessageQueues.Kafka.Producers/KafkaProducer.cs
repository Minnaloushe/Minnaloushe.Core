using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.ClientProviders.Kafka;
using Minnaloushe.Core.ClientProviders.Kafka.Options;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.Abstractions.Routines;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Extensions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Kafka.Extensions;
using Minnaloushe.Core.Toolbox.RetryRoutines;
using Polly;
using Polly.Retry;
using System.Text;
using System.Text.Json;

namespace Minnaloushe.Core.MessageQueues.Kafka.Producers;

internal class KafkaProducer<TMessage> : IProducer<TMessage>
    where TMessage : class
{
    private static readonly ResiliencePropertyKey<TMessage?> MessagePropertyKey = new("Message");

    private readonly IKafkaProducerClientProvider _clientProvider;
    private readonly IKafkaAdminClientProvider _adminClientProvider;
    private readonly IOptionsMonitor<MessageQueueOptions> _optionsMonitor;
    private readonly ILogger<KafkaProducer<TMessage>> _logger;
    private readonly ResiliencePipeline _retryPolicy;
    private readonly HashSet<string> _declaredTopics = [];
    private readonly Lock _topicsLock = new();
    private readonly string _producerName;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Func<TMessage, string>? _keySelector;
    private readonly IMessageQueueNamingConventionsProvider _namingConventionsProvider;
    private readonly bool _runtimeTopicResolution;
    private readonly string _staticTopicName;
    private readonly bool _keyRequired;

    public KafkaProducer(
        IKafkaProducerClientProvider clientProvider,
        IMessageQueueNamingConventionsProvider namingConventionsProvider,
        IKafkaAdminClientProvider adminClientProvider,
        IOptionsMonitor<MessageQueueOptions> optionsMonitor,
        JsonSerializerOptions jsonOptions,
        ILogger<KafkaProducer<TMessage>> logger,
        string producerName,
        ProducerOptions<TMessage> producerOptions = null!)
    {
        _clientProvider = clientProvider;
        _adminClientProvider = adminClientProvider;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
        _producerName = producerName;
        _jsonOptions = jsonOptions;
        _namingConventionsProvider = namingConventionsProvider;
        _keySelector = producerOptions?.KeySelector;
        _staticTopicName = namingConventionsProvider.GetTopicName<TMessage>();
        _runtimeTopicResolution = producerOptions?.ResolveMessageTypeAtRuntime ?? false;


        var options = _optionsMonitor.Get(_producerName);

        _keyRequired = (options.ToClientOptions().Parameters.TopicConfiguration.CleanUpPolicy &
                       CleanUpPolicy.Compact) != 0;

        _retryPolicy = new ResiliencePipelineBuilder()
            //TODO move to config
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
                        var topicName = GetTopicName(message);

                        _logger.LogWarning(args.Outcome.Exception,
                            "Failed to publish to topic '{Topic}' (attempt {Attempt}/{Retries}), " +
                            "trying to ensure topic exists...",
                            topicName, args.AttemptNumber, options.RetryPolicy.MaxRetries);

                        try
                        {
                            await EnsureTopicExistsAsync(topicName);
                        }
                        catch (ProduceException<byte[], byte[]> ex)
                        {
                            _logger.LogError(ex, "Failed to ensure topic exists before retrying: {Topic}", topicName);
                        }
                    },
                    ShouldHandle = args
                        => ValueTask.FromResult(args.Outcome.Exception is ProduceException<byte[], byte[]>)
                }
            )
            .Build();
    }

    private string GetTopicName(TMessage? message)
    {
        return
            _runtimeTopicResolution && message is not null
                ? _namingConventionsProvider.GetTopicName(message.GetType())
                : _staticTopicName;
    }

    public async Task PublishAsync(
        TMessage? message,
        string? key = null,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default
        )
    {
        var context = ResilienceContextPool.Shared.Get(cancellationToken);
        context.Properties.Set(MessagePropertyKey, message);

        try
        {
            await _retryPolicy.ExecuteAsync(async (ctx) =>
            {
                using var lease = _clientProvider.Acquire();

                var effectiveKey = key ?? (message is not null && _keySelector is not null ? _keySelector(message) : null);

                if (effectiveKey is null && _keyRequired)
                {
                    effectiveKey = Guid.NewGuid().ToString("N");
                }

                if (effectiveKey is null && message is null)
                {
                    //TODO Consider throwing an exception here instead of just logging, as this is likely a mistake by the caller.
                    // Kafka allows null keys and values, but having both as null makes the message effectively useless.
                    _logger.LogWarning("Trying to publish message {MessageType} with null key and message", typeof(TMessage).FullName);
                    return;
                }

                var keyBytes = effectiveKey is null
                    ? null
                    : Encoding.UTF8.GetBytes(effectiveKey);
                var value = message is null
                    ? null
                    : _runtimeTopicResolution
                        ? JsonSerializer.SerializeToUtf8Bytes(message, message.GetType(), _jsonOptions)
                        : JsonSerializer.SerializeToUtf8Bytes(message, _jsonOptions);

                var kafkaMessage = new Message<byte[], byte[]>
                {
                    Key = keyBytes!,
                    Value = value!,
                    Headers = CreateKafkaHeaders(headers)
                };

                var topicName = GetTopicName(message);

                //A bit of overhead, but helps to ensure messages are not lost due to missing topics, especially in dynamic topic resolution scenarios.
                await EnsureTopicExistsAsync(topicName);

                _logger.LogDebug("Sending message to {TopicName}", topicName);
                await lease.Client.Producer.ProduceAsync(topicName, kafkaMessage, ctx.CancellationToken);
                _logger.LogDebug("Message to {TopicName} has been sent", topicName);
            }, context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message of type {MessageType} to topic {TopicName}",
                typeof(TMessage).FullName, GetTopicName(message));
            throw;
        }

        finally
        {
            ResilienceContextPool.Shared.Return(context);
        }
    }

    private async Task EnsureTopicExistsAsync(string topicName)
    {
        lock (_topicsLock)
        {
            if (_declaredTopics.Contains(topicName))
            {
                return;
            }
        }

        using var adminLease = _adminClientProvider.Acquire();
        var adminClient = adminLease.Client.Client;

        try
        {
            var options = _optionsMonitor.Get(_producerName);
            var kafkaParameters = options.ToClientOptions().Parameters;

            await adminClient.CreateTopicsAsync([kafkaParameters.TopicConfiguration.ToTopicSpecification(topicName)]);

            lock (_topicsLock)
            {
                _declaredTopics.Add(topicName);
            }

            _logger.LogInformation("Producer topic '{TopicName}' created on demand", topicName);
        }
        catch (CreateTopicsException ex) when (ex.Results.Any(r => r.Error.Code == ErrorCode.TopicAlreadyExists))
        {
            lock (_topicsLock)
            {
                _declaredTopics.Add(topicName);
            }

            _logger.LogDebug("Producer topic '{TopicName}' already exists", topicName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create producer topic '{TopicName}'", topicName);
            throw;
        }
    }

    private static Headers? CreateKafkaHeaders(IReadOnlyDictionary<string, string>? headers)
    {
        if (headers is null or { Count: 0 })
        {
            return null;
        }

        var kafkaHeaders = new Headers();
        foreach (var (key, value) in headers)
        {
            kafkaHeaders.Add(key, Encoding.UTF8.GetBytes(value));
        }

        return kafkaHeaders;
    }
}

