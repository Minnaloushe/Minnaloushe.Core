using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.ClientProviders.Kafka;
using Minnaloushe.Core.ClientProviders.Kafka.Options;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.Abstractions.ErrorHandling;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Kafka.Extensions;
using Minnaloushe.Core.Toolbox.RetryRoutines;
using Polly;
using Polly.Retry;
using System.Text;

namespace Minnaloushe.Core.MessageQueues.Kafka.Consumers.ErrorHandling;

/// <summary>
/// Kafka implementation of dead letter publisher.
/// Creates DLT topics on demand if they don't exist using the admin client.
/// </summary>
internal sealed class KafkaDeadLetterPublisher : IDeadLetterPublisher
{
    private readonly HashSet<string> _declaredTopics = [];
    private readonly Lock _topicsLock = new();
    private readonly string _consumerName;
    private readonly IKafkaAdminClientProvider _adminClientProvider;
    private readonly IKafkaProducerClientProvider _producerClientProvider;
    private readonly IOptionsMonitor<MessageQueueOptions> _optionsMonitor;
    private readonly ILogger<KafkaDeadLetterPublisher> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;

    /// <summary>
    /// Kafka implementation of dead letter publisher.
    /// Creates DLT topics on demand if they don't exist using the admin client.
    /// </summary>
    public KafkaDeadLetterPublisher(string consumerName,
        IKafkaAdminClientProvider adminClientProvider,
        IKafkaProducerClientProvider producerClientProvider,
        IOptionsMonitor<MessageQueueOptions> optionsMonitor,
        ILogger<KafkaDeadLetterPublisher> logger)
    {
        _consumerName = consumerName;
        _adminClientProvider = adminClientProvider;
        _producerClientProvider = producerClientProvider;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
        Options = optionsMonitor.Get(consumerName);
        // Initialize retry policy once to avoid recreating it on every publish call.
        _retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                Options.RetryPolicy.MaxRetries,
                Options.RetryPolicy.GetDelay,
                async (exception, _, retryCount, context) =>
                {
                    // Try to get topic name from Polly context
                    var topicName = context.TryGetValue("topicName", out var value) ? value?.ToString() ?? string.Empty : string.Empty;

                    _logger.LogFailedToPublishToDeadLetter(topicName, retryCount, Options.RetryPolicy.MaxRetries, exception);

                    try
                    {
                        // Use CancellationToken.None here because retry callback doesn't receive the original token.
                        await EnsureTopicExistsAsync(topicName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogFailedToEnsureTopicExistsBeforeRetry(topicName, ex);
                    }
                });
    }

    private MessageQueueOptions Options { get; }

    public async Task PublishToDeadLetterAsync(
        string deadLetterDestination,
        FailedMessageDetails details,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken)
    {
        // Execute publish with previously initialized retry policy. Pass topic in Polly context so the
        // retry callback can try to ensure the topic exists before retrying.

        await _retryPolicy.ExecuteAsync(async (_, ct) =>
            await PublishInternal(deadLetterDestination, details, headers, ct),
            new Context
            {
                ["topicName"] = deadLetterDestination
            },
            cancellationToken);
    }

    private async Task PublishInternal(string deadLetterDestination, FailedMessageDetails details,
        IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken)
    {
        var kafkaHeaders = new Headers();
        foreach (var (key, value) in headers)
        {
            kafkaHeaders.Add(key, Encoding.UTF8.GetBytes(value));
        }

        var message = new Message<byte[], byte[]>
        {
            Key = Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()),
            Value = details.OriginalMessage.ToArray(),
            Headers = kafkaHeaders
        };

        using var producerLease = _producerClientProvider.Acquire();

        await EnsureTopicExistsAsync(deadLetterDestination);

        await producerLease.Client.Producer.ProduceAsync(deadLetterDestination, message, cancellationToken);

        _logger.LogMessageSentToDeadLetter(deadLetterDestination, details.Topic);
    }

    private async Task EnsureTopicExistsAsync(string topicName)
    {
        // Check if we've already created this topic in this session
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
            var options = _optionsMonitor.Get(_consumerName);
            var kafkaParameters = options.ToClientOptions().Parameters;

            await adminClient.CreateTopicsAsync(
            [
                kafkaParameters.DltTopicConfiguration.ToTopicSpecification(topicName)
            ]);

            lock (_topicsLock)
            {
                _declaredTopics.Add(topicName);
            }

            _logger.LogDeadLetterTopicCreated(topicName);
        }
        catch (CreateTopicsException ex) when (ex.Results.Any(r => r.Error.Code == ErrorCode.TopicAlreadyExists))
        {
            // Topic was created by another process/thread, that's fine
            lock (_topicsLock)
            {
                _declaredTopics.Add(topicName);
            }

            _logger.LogDeadLetterTopicAlreadyExists(topicName);
        }
        catch (Exception ex)
        {
            _logger.LogFailedToCreateDeadLetterTopic(topicName, ex);
            throw;
        }
    }
}
