using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Minnaloushe.Core.ClientProviders.Kafka;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Extensions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Kafka.Extensions;
using Minnaloushe.Core.MessageQueues.Kafka.Consumers.Extensions;
using Minnaloushe.Core.MessageQueues.Routines;
using Minnaloushe.Core.Tests.Helpers;
using Minnaloushe.Core.Toolbox.AsyncInitializer.Extensions;
using Minnaloushe.Core.Toolbox.JsonConfiguration;
using Minnaloushe.Core.Toolbox.TestHelpers;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Minnaloushe.Core.MessageQueues.Kafka.Tests.Integration;

/// <summary>
/// Integration tests for Kafka Dead Letter Topic (DLT) error handling.
/// Tests that failed messages are properly sent to the dead letter topic.
/// </summary>
[TestFixture]
[Category("TestContainers")]
[Category("Integration")]
public class KafkaDeadLetterIntegrationTests
{
    private TestHost _testHost = null!;

    public static readonly AsyncThresholdCollection<DltTestMessage> ReceivedMessages = new();
    public static readonly AsyncThresholdCollection<DltTestMessage> FailedMessages = new();

    private readonly string _connectionName = Helpers.UniqueString("kafka-dlt-connection");
    private readonly string _serviceKey = Helpers.UniqueString("kafka-dlt-service");
    private readonly string _consumerName = Helpers.UniqueString("dlt-test-consumer");
    private readonly string _topicName = MqNaming.GetSafeName<DltTestMessage>();
    private readonly string _dltTopicName = $"{MqNaming.GetSafeName<DltTestMessage>()}.dlt";
    // Control whether the handler should fail
    public static bool ShouldFail { get; set; } = false;
    public static bool ShouldThrow { get; set; } = false;

    private object AppSettings => MqHelpers.CreateAppSettings(
        [
            MqHelpers.CreateConnection(
                _connectionName,
                type: "kafka",
                connectionString: GlobalFixture.Kafka1.Instance.GetBootstrapAddress(), serviceKey: _serviceKey)
        ],
        [
            MqHelpers.CreateConsumer(
                _consumerName,
                _connectionName,
                "DeadLetter"
            )
        ]
    );

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        ReceivedMessages.Clear();
        FailedMessages.Clear();
        ShouldFail = false;
        ShouldThrow = false;

        _testHost = await TestHost.Build(
            configureConfiguration: cfg =>
            {
                cfg.AddConfiguration(AppSettings);
            },
            configureServices: (services, configuration) =>
            {
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Debug);
                });
                services.AddJsonConfiguration();
                services.ConfigureAsyncInitializers();

                services.AddMessageQueues(configuration)
                    .AddKafkaClientProviders()
                    .AddKafkaConsumers()
                    .AddConsumer<DltTestMessage, DltTestConsumerHandler>(_consumerName)
                    .Build();
            },
            beforeStart: async (host) =>
            {
                await host.InvokeAsyncInitializers();
            },
            startHost: true
        );

        // Give time for consumer initialization and topic creation
        await WaitForTopicCreated(MqNaming.GetSafeName<DltTestMessage>(), 10);
    }

    private async Task WaitForTopicCreated(string topicName, int waitForSeconds)
    {
        var admin = _testHost.Services.GetRequiredKeyedService<IKafkaAdminClientProvider>(_connectionName);

        using var lease = admin.Acquire();


        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(waitForSeconds))
        {
            var metadata = lease.Client.Client.GetMetadata(TimeSpan.FromSeconds(2));
            if (metadata.Topics.Any(t => t.Topic == topicName))
            {
                return;
            }
            await Task.Delay(500);
        }
        throw new TimeoutException($"Topic '{topicName}' was not created within the expected time.");
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _testHost.DisposeAsync();
    }

    [SetUp]
    public void Setup()
    {
        ReceivedMessages.Clear();
        FailedMessages.Clear();
        ShouldFail = false;
        ShouldThrow = false;
    }

    [Test]
    public async Task PublishAsync_WhenHandlerReturnsFalse_ThenShouldSendToDeadLetterTopic()
    {
        // Arrange
        ShouldFail = true; // Configure handler to return false

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = GlobalFixture.Kafka1.Instance.GetBootstrapAddress(),
            SecurityProtocol = SecurityProtocol.Plaintext,
            SaslMechanism = SaslMechanism.Plain,
            SaslUsername = GlobalFixture.Kafka1.Username,
            SaslPassword = GlobalFixture.Kafka1.Password
        };


        var messageContent = $"FailedMessage-{Guid.NewGuid()}";
        var message = new DltTestMessage { Content = messageContent };
        var messageJson = JsonSerializer.Serialize(message);

        // Act - publish message that will fail processing
        using (var producer = new ProducerBuilder<string, string>(producerConfig).Build())
        {
            await producer.ProduceAsync(_topicName, new Message<string, string>
            {
                Key = Guid.NewGuid().ToString(),
                Value = messageJson
            });
        }

        // Wait for message to be processed (and fail)
        await FailedMessages.WaitUntilCountAtLeastAsync(1, TimeSpan.FromSeconds(15));

        // Assert - verify message was sent to DLT
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = GlobalFixture.Kafka1.Instance.GetBootstrapAddress(),
            SecurityProtocol = SecurityProtocol.Plaintext,
            SaslMechanism = SaslMechanism.Plain,
            SaslUsername = GlobalFixture.Kafka1.Username,
            SaslPassword = GlobalFixture.Kafka1.Password,
            GroupId = $"dlt-verify-{Guid.NewGuid()}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        using var dltConsumer = new ConsumerBuilder<byte[], byte[]>(consumerConfig).Build();
        dltConsumer.Subscribe(_dltTopicName);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        ConsumeResult<byte[], byte[]>? dltMessage = null;

        while (!cts.IsCancellationRequested)
        {
            try
            {
                dltMessage = dltConsumer.Consume(cts.Token);
                if (dltMessage?.Message?.Value != null)
                {
                    var dltContent = Encoding.UTF8.GetString(dltMessage.Message.Value);
                    if (dltContent.Contains(messageContent))
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        dltConsumer.Close();

        using var scope = new AssertionScope();

        dltMessage.Should().NotBeNull("Message should be found in DLT");
        var dltMessageContent = Encoding.UTF8.GetString(dltMessage!.Message.Value);
        dltMessageContent.Should().Contain(messageContent, "DLT message should contain original message content");
    }

    [Test]
    public async Task PublishAsync_WhenHandlerThrowsException_ThenShouldSendToDeadLetterTopic()
    {
        // Arrange
        ShouldThrow = true; // Configure handler to throw exception

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = GlobalFixture.Kafka1.Instance.GetBootstrapAddress(),
            SecurityProtocol = SecurityProtocol.Plaintext,
            SaslMechanism = SaslMechanism.Plain,
            SaslUsername = GlobalFixture.Kafka1.Username,
            SaslPassword = GlobalFixture.Kafka1.Password
        };

        var messageContent = $"ExceptionMessage-{Guid.NewGuid()}";
        var message = new DltTestMessage { Content = messageContent };
        var messageJson = JsonSerializer.Serialize(message);

        // Act - publish message that will cause exception
        using (var producer = new ProducerBuilder<string, string>(producerConfig).Build())
        {
            await producer.ProduceAsync(_topicName, new Message<string, string>
            {
                Key = Guid.NewGuid().ToString(),
                Value = messageJson
            });
        }

        // Wait for message to be processed (and throw)
        await FailedMessages.WaitUntilCountAtLeastAsync(1, TimeSpan.FromSeconds(15));

        await WaitForTopicCreated(_dltTopicName, 10);

        // Assert - verify message was sent to DLT
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = GlobalFixture.Kafka1.Instance.GetBootstrapAddress(),
            SecurityProtocol = SecurityProtocol.Plaintext,
            SaslMechanism = SaslMechanism.Plain,
            SaslUsername = GlobalFixture.Kafka1.Username,
            SaslPassword = GlobalFixture.Kafka1.Password,
            GroupId = $"dlt-verify-exception-{Guid.NewGuid()}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        using var dltConsumer = new ConsumerBuilder<byte[], byte[]>(consumerConfig).Build();
        dltConsumer.Subscribe(_dltTopicName);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        ConsumeResult<byte[], byte[]>? dltMessage = null;

        while (!cts.IsCancellationRequested)
        {
            try
            {
                dltMessage = dltConsumer.Consume(cts.Token);
                if (dltMessage?.Message?.Value != null)
                {
                    var dltContent = Encoding.UTF8.GetString(dltMessage.Message.Value);
                    if (dltContent.Contains(messageContent))
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        dltConsumer.Close();

        using var scope = new AssertionScope();

        dltMessage.Should().NotBeNull("Message should be found in DLT after exception");
        var dltMessageContent = Encoding.UTF8.GetString(dltMessage!.Message.Value);
        dltMessageContent.Should().Contain(messageContent, "DLT message should contain original message content");
    }

    [Test]
    public async Task PublishAsync_WhenHandlerSucceeds_ThenShouldNotSendToDeadLetterTopic()
    {
        // Arrange - handler will succeed (ShouldFail = false by default)
        var producerConfig = new ProducerConfig
        {
            BootstrapServers = GlobalFixture.Kafka1.Instance.GetBootstrapAddress(),
            SecurityProtocol = SecurityProtocol.Plaintext,
            SaslMechanism = SaslMechanism.Plain,
            SaslUsername = GlobalFixture.Kafka1.Username,
            SaslPassword = GlobalFixture.Kafka1.Password
        };

        var messageContent = Helpers.UniqueString("SuccessMessage");
        var message = new DltTestMessage { Content = messageContent };
        var messageJson = JsonSerializer.Serialize(message);

        // Act - publish message that will succeed
        using (var producer = new ProducerBuilder<string, string>(producerConfig).Build())
        {
            await producer.ProduceAsync(_topicName, new Message<string, string>
            {
                Key = Guid.NewGuid().ToString(),
                Value = messageJson
            });
        }

        // Wait for message to be successfully processed
        var received = await ReceivedMessages.WaitUntilCountAtLeastAsync(1, TimeSpan.FromSeconds(15));
        received.Should().BeTrue("Message should be successfully processed");


        await WaitForTopicCreated(_dltTopicName, 10);

        // Assert - verify message was NOT sent to DLT
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = GlobalFixture.Kafka1.Instance.GetBootstrapAddress(),
            SecurityProtocol = SecurityProtocol.Plaintext,
            SaslMechanism = SaslMechanism.Plain,
            SaslUsername = GlobalFixture.Kafka1.Username,
            SaslPassword = GlobalFixture.Kafka1.Password,
            GroupId = Helpers.UniqueString("dlt-verify-success"),
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        using var dltConsumer = new ConsumerBuilder<byte[], byte[]>(consumerConfig).Build();
        dltConsumer.Subscribe(_dltTopicName);

        // Try to consume from DLT with short timeout - should NOT find the message
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        ConsumeResult<byte[], byte[]>? dltMessage = null;

        while (!cts.IsCancellationRequested)
        {
            try
            {
                dltMessage = dltConsumer.Consume(cts.Token);
                if (dltMessage?.Message?.Value != null)
                {
                    var dltContent = Encoding.UTF8.GetString(dltMessage.Message.Value);
                    if (dltContent.Contains(messageContent))
                    {
                        // Found the message - this is unexpected
                        break;
                    }
                }
                dltMessage = null; // Reset if message doesn't match
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        dltConsumer.Close();

        // The specific message should NOT be in DLT
        if (dltMessage?.Message?.Value != null)
        {
            var dltContent = Encoding.UTF8.GetString(dltMessage.Message.Value);
            dltContent.Should().NotContain(messageContent, "Successfully processed message should NOT be in DLT");
        }
    }

    [Test]
    public async Task DltMessage_WhenPublished_ThenShouldContainOriginalHeaders()
    {
        // Arrange
        ShouldFail = true; // Configure handler to fail

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = GlobalFixture.Kafka1.Instance.GetBootstrapAddress()
        };

        var messageContent = $"HeaderTestMessage-{Guid.NewGuid()}";
        var correlationId = Guid.NewGuid().ToString();
        var message = new DltTestMessage { Content = messageContent };
        var messageJson = JsonSerializer.Serialize(message);

        var originalHeaders = new Headers
        {
            { "x-correlation-id", Encoding.UTF8.GetBytes(correlationId) },
            { "x-custom-header", Encoding.UTF8.GetBytes("test-value") }
        };

        // Act - publish message with headers that will fail processing
        using (var producer = new ProducerBuilder<string, string>(producerConfig).Build())
        {
            await producer.ProduceAsync(_topicName, new Message<string, string>
            {
                Key = Guid.NewGuid().ToString(),
                Value = messageJson,
                Headers = originalHeaders
            });
        }

        // Wait for message to be processed (and fail)
        await FailedMessages.WaitUntilCountAtLeastAsync(1, TimeSpan.FromSeconds(15));

        await WaitForTopicCreated(_dltTopicName, 10);

        // Assert - verify DLT message contains original headers
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = GlobalFixture.Kafka1.Instance.GetBootstrapAddress(),
            GroupId = $"dlt-verify-headers-{Guid.NewGuid()}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        using var dltConsumer = new ConsumerBuilder<byte[], byte[]>(consumerConfig).Build();
        dltConsumer.Subscribe(_dltTopicName);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        ConsumeResult<byte[], byte[]>? dltMessage = null;

        while (!cts.IsCancellationRequested)
        {
            try
            {
                dltMessage = dltConsumer.Consume(cts.Token);
                if (dltMessage?.Message?.Value != null)
                {
                    var dltContent = Encoding.UTF8.GetString(dltMessage.Message.Value);
                    if (dltContent.Contains(messageContent))
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        dltConsumer.Close();

        using var scope = new AssertionScope();

        dltMessage.Should().NotBeNull("Message should be found in DLT");
        dltMessage!.Message.Should().NotBeNull();
        dltMessage!.Message.Headers.Should().NotBeNull("DLT message should have headers");

        // Check for original headers (they might be preserved or prefixed)
        var headerKeys = dltMessage.Message.Headers.Select(h => h.Key).ToList();

        // The DLT publisher should preserve original headers
        // Check if correlation-id is present (either as original or prefixed)
        var hasCorrelationHeader = headerKeys.Any(k =>
            k.Contains("correlation-id", StringComparison.OrdinalIgnoreCase));

        hasCorrelationHeader.Should().BeTrue("DLT message should contain correlation header");
    }
}

/// <summary>
/// Test message class for DLT tests.
/// </summary>
public class DltTestMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Content { get; init; } = string.Empty;
}

/// <summary>
/// Test consumer handler that can be configured to fail for DLT testing.
/// </summary>
public class DltTestConsumerHandler(ILogger<DltTestConsumerHandler> logger) : IConsumer<DltTestMessage>
{
    public Task<bool> HandleMessageAsync(MessageEnvelop<DltTestMessage> envelop, CancellationToken cancellationToken = default)
    {
#pragma warning disable CA1873
        logger.LogInformation("DLT test consumer received message: {Content}, ShouldFail: {ShouldFail}, ShouldThrow: {ShouldThrow}",
#pragma warning restore CA1873
            envelop.Message.Content, KafkaDeadLetterIntegrationTests.ShouldFail, KafkaDeadLetterIntegrationTests.ShouldThrow);

        if (KafkaDeadLetterIntegrationTests.ShouldThrow)
        {
            KafkaDeadLetterIntegrationTests.FailedMessages.Add(envelop.Message);
            throw new InvalidOperationException($"Intentional test exception for message: {envelop.Message.Content}");
        }

        if (KafkaDeadLetterIntegrationTests.ShouldFail)
        {
            KafkaDeadLetterIntegrationTests.FailedMessages.Add(envelop.Message);
            logger.LogWarning("Handler returning false for message: {Content}", envelop.Message.Content);
            return Task.FromResult(false);
        }

        KafkaDeadLetterIntegrationTests.ReceivedMessages.Add(envelop.Message);
#pragma warning disable CA1873
        logger.LogInformation("Handler successfully processed message: {Content}", envelop.Message.Content);
#pragma warning restore CA1873
        return Task.FromResult(true);
    }
}
