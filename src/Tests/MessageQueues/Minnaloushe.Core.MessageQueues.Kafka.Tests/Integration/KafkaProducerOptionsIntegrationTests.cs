using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Extensions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Kafka.Extensions;
using Minnaloushe.Core.MessageQueues.Kafka.Producers.Extensions;
using Minnaloushe.Core.MessageQueues.Routines;
using Minnaloushe.Core.Tests.Helpers;
using Minnaloushe.Core.Toolbox.AsyncInitializer.Extensions;
using Minnaloushe.Core.Toolbox.JsonConfiguration;
using Minnaloushe.Core.Toolbox.TestHelpers;

namespace Minnaloushe.Core.MessageQueues.Kafka.Tests.Integration;

/// <summary>
/// Integration tests for Kafka producer functionality.
/// Tests producer registration, message publishing, and header handling.
/// </summary>
[TestFixture]
[Category("TestContainers")]
[Category("Integration")]
public class KafkaProducerOptionsIntegrationTests
{
    #region Fixture members

    #region Fields

    private TestHost _testHost = null!;
    private readonly string _serviceKey = Helpers.UniqueString("test-topic");
    private readonly string _connectionName = Helpers.UniqueString("kafka-producer-connection");
    private readonly string _producer1Name = Helpers.UniqueString("producer-1");
    private readonly string _producer2Name = Helpers.UniqueString("producer-2");

    #endregion

    #region Properties

    private object AppSettings => MqHelpers.CreateAppSettings(
        [
            MqHelpers.CreateConnection(
                _connectionName,
                type: "kafka", connectionString: GlobalFixture.Kafka1.Instance.GetBootstrapAddress(), serviceKey: _serviceKey)
        ],
        []
    );

    #endregion

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
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
                    builder.SetMinimumLevel(LogLevel.Information);
                });
                services.AddJsonConfiguration();

                services.ConfigureAsyncInitializers();

                services.AddMessageQueues(configuration)
                    .AddKafkaClientProviders()
                    .AddKafkaProducers()
                    .AddProducer<ProducerTestMessage>(_connectionName, _producer1Name, new ProducerOptions<ProducerTestMessage>()
                    {
                        KeySelector = m => m.Id.ToString(),
                    })
                    .AddProducer<ProducerTestMessage2>(_connectionName, _producer2Name, new ProducerOptions<ProducerTestMessage2>()
                    {
                        ResolveMessageTypeAtRuntime = true,
                        KeySelector = m => m.Id.ToString(),
                    })
                    .Build();
            },
            beforeStart: async (host) =>
            {
                await host.InvokeAsyncInitializers();
            },
            startHost: true
        );
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _testHost.DisposeAsync();
    }

    #endregion


    #region Tests

    [Test]
    public async Task PublishAsync_WhenKeySelectorConfigured_ThenShouldUseExtractedKey()
    {
        // Arrange
        var producer = _testHost.Services.GetRequiredKeyedService<IProducer<ProducerTestMessage>>(_producer1Name);
        var messageId = Guid.NewGuid();
        var message = new ProducerTestMessage { Id = messageId, Content = $"KeySelectorTest-{Guid.NewGuid()}" };

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = GlobalFixture.Kafka1.Instance.GetBootstrapAddress(),
            SecurityProtocol = SecurityProtocol.Plaintext,
            SaslMechanism = SaslMechanism.Plain,
            SaslUsername = GlobalFixture.Kafka1.Username,
            SaslPassword = GlobalFixture.Kafka1.Password,
            GroupId = $"test-group-{Guid.NewGuid()}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        // Act
        await producer.PublishAsync(message, cancellationToken: CancellationToken.None);

        // Assert
        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        var topicName = MqNaming.GetSafeName<ProducerTestMessage>();
        consumer.Subscribe(topicName);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        ConsumeResult<string, string>? consumeResult = null;

        while (!cts.IsCancellationRequested)
        {
            try
            {
                consumeResult = consumer.Consume(cts.Token);
                if (consumeResult?.Message?.Value?.Contains(messageId.ToString()) == true)
                {
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        consumer.Close();

        using var scope = new AssertionScope();

        consumeResult.Should().NotBeNull("Message should be consumed from Kafka");
        consumeResult.Message.Should().NotBeNull();
        consumeResult.Message.Key.Should().Be(messageId.ToString(), "Key should be extracted using KeySelector");
        consumeResult.Message.Value.Should().Contain(messageId.ToString(), "Consumed message should contain expected data");
    }

    [Test]
    public async Task PublishAsync_WhenKeySelectorConfigured_ThenDifferentMessagesShouldHaveDifferentKeys()
    {
        // Arrange
        var producer = _testHost.Services.GetRequiredKeyedService<IProducer<ProducerTestMessage>>(_producer1Name);
        var message1Id = Guid.NewGuid();
        var message2Id = Guid.NewGuid();
        var message1 = new ProducerTestMessage { Id = message1Id, Content = $"MultiKeyTest1-{Guid.NewGuid()}" };
        var message2 = new ProducerTestMessage { Id = message2Id, Content = $"MultiKeyTest2-{Guid.NewGuid()}" };

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = GlobalFixture.Kafka1.Instance.GetBootstrapAddress(),
            SecurityProtocol = SecurityProtocol.Plaintext,
            SaslMechanism = SaslMechanism.Plain,
            SaslUsername = GlobalFixture.Kafka1.Username,
            SaslPassword = GlobalFixture.Kafka1.Password,
            GroupId = $"test-group-{Guid.NewGuid()}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        // Act
        await producer.PublishAsync(message1, cancellationToken: CancellationToken.None);
        await producer.PublishAsync(message2, cancellationToken: CancellationToken.None);

        // Assert
        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        var topicName = MqNaming.GetSafeName<ProducerTestMessage>();
        consumer.Subscribe(topicName);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var keys = new List<string>();

        while (!cts.IsCancellationRequested && keys.Count < 2)
        {
            try
            {
                var consumeResult = consumer.Consume(cts.Token);
                if (consumeResult?.Message?.Value?.Contains("MultiKeyTest") == true)
                {
                    keys.Add(consumeResult.Message.Key);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        consumer.Close();

        using var scope = new AssertionScope();

        keys.Should().HaveCount(2, "Both messages should be consumed");
        keys.Should().Contain(message1Id.ToString(), "First message should have its ID as key");
        keys.Should().Contain(message2Id.ToString(), "Second message should have its ID as key");
        keys[0].Should().NotBe(keys[1], "Different messages should have different keys");
    }

    [Test]
    public async Task PublishAsync_WhenRuntimeTypeResolutionEnabled_ThenShouldUseRuntimeType()
    {
        // Arrange
        var producer = _testHost.Services.GetRequiredKeyedService<IProducer<ProducerTestMessage2>>(_producer2Name);
        var message = new ProducerTestMessage2Descendant
        {
            Id = Guid.NewGuid(),
            Value = $"RuntimeTypeTest-{Guid.NewGuid()}",
            Extra = "ExtraData"
        };

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = GlobalFixture.Kafka1.Instance.GetBootstrapAddress(),
            SecurityProtocol = SecurityProtocol.Plaintext,
            SaslMechanism = SaslMechanism.Plain,
            SaslUsername = GlobalFixture.Kafka1.Username,
            SaslPassword = GlobalFixture.Kafka1.Password,
            GroupId = $"test-group-{Guid.NewGuid()}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        // Act
        await producer.PublishAsync(message, cancellationToken: CancellationToken.None);

        // Assert
        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        var runtimeTopicName = MqNaming.GetSafeName<ProducerTestMessage2Descendant>();
        consumer.Subscribe(runtimeTopicName);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        ConsumeResult<string, string>? consumeResult = null;

        while (!cts.IsCancellationRequested)
        {
            try
            {
                consumeResult = consumer.Consume(cts.Token);
                if (consumeResult?.Message?.Value?.Contains(message.Value) == true)
                {
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        consumer.Close();

        using var scope = new AssertionScope();

        consumeResult.Should().NotBeNull("Message should be consumed from runtime-typed topic");
        consumeResult.Message.Should().NotBeNull();
        consumeResult.Message.Value.Should().Contain(message.Value, "Message should contain expected value");
        consumeResult.Message.Value.Should().Contain(message.Extra, "Message should contain descendant-specific data");
    }

    [Test]
    public async Task PublishAsync_WhenRuntimeTypeResolutionEnabled_ThenShouldNotPublishToStaticTypeTopic()
    {
        // Arrange
        var producer = _testHost.Services.GetRequiredKeyedService<IProducer<ProducerTestMessage2>>(_producer2Name);
        var uniqueMarker = $"StaticTypeTest-{Guid.NewGuid()}";

        // First, publish a base type message to ensure the static topic exists
        var baseMessage = new ProducerTestMessage2
        {
            Id = Guid.NewGuid(),
            Value = $"BaseMessage-{Guid.NewGuid()}"
        };
        await producer.PublishAsync(baseMessage, cancellationToken: CancellationToken.None);

        var staticTopicName = MqNaming.GetSafeName<ProducerTestMessage2>();
        await KafkaHelpers.WaitForTopicCreation(GlobalFixture.Kafka1, staticTopicName);

        // Now publish the descendant message
        var message = new ProducerTestMessage2Descendant
        {
            Id = Guid.NewGuid(),
            Value = uniqueMarker,
            Extra = "ExtraData"
        };

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = GlobalFixture.Kafka1.Instance.GetBootstrapAddress(),
            SecurityProtocol = SecurityProtocol.Plaintext,
            SaslMechanism = SaslMechanism.Plain,
            SaslUsername = GlobalFixture.Kafka1.Username,
            SaslPassword = GlobalFixture.Kafka1.Password,
            GroupId = $"test-group-{Guid.NewGuid()}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        // Act
        await producer.PublishAsync(message, cancellationToken: CancellationToken.None);

        // Assert
        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        consumer.Subscribe(staticTopicName);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var foundInStaticTopic = false;

        while (!cts.IsCancellationRequested)
        {
            try
            {
                var consumeResult = consumer.Consume(cts.Token);
                if (consumeResult?.Message?.Value?.Contains(uniqueMarker) == true)
                {
                    foundInStaticTopic = true;
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        consumer.Close();

        foundInStaticTopic.Should().BeFalse(
            "Message should not be found in static type topic when runtime type resolution is enabled");
    }

    [Test]
    public async Task PublishAsync_WhenRuntimeTypeResolutionEnabled_WithBaseType_ThenShouldUseBaseTypeTopic()
    {
        // Arrange
        var producer = _testHost.Services.GetRequiredKeyedService<IProducer<ProducerTestMessage2>>(_producer2Name);
        var message = new ProducerTestMessage2
        {
            Id = Guid.NewGuid(),
            Value = $"BaseTypeTest-{Guid.NewGuid()}"
        };

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = GlobalFixture.Kafka1.Instance.GetBootstrapAddress(),
            SecurityProtocol = SecurityProtocol.Plaintext,
            SaslMechanism = SaslMechanism.Plain,
            SaslUsername = GlobalFixture.Kafka1.Username,
            SaslPassword = GlobalFixture.Kafka1.Password,
            GroupId = $"test-group-{Guid.NewGuid()}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        // Act
        await producer.PublishAsync(message, cancellationToken: CancellationToken.None);

        // Assert
        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        var baseTopicName = MqNaming.GetSafeName<ProducerTestMessage2>();
        consumer.Subscribe(baseTopicName);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        ConsumeResult<string, string>? consumeResult = null;

        while (!cts.IsCancellationRequested)
        {
            try
            {
                consumeResult = consumer.Consume(cts.Token);
                if (consumeResult?.Message?.Value?.Contains(message.Value) == true)
                {
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        consumer.Close();

        using var scope = new AssertionScope();

        consumeResult.Should().NotBeNull(
            "When publishing base type with runtime resolution, message should go to base type topic");
        consumeResult.Message.Should().NotBeNull();
        consumeResult.Message.Value.Should().Contain(message.Value, "Message should contain expected value");
    }

    #endregion


    #region Helper classes

    /// <summary>
    /// Test message class for producer tests.
    /// </summary>
    public class ProducerTestMessage
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public string Content { get; init; } = string.Empty;
    }

    /// <summary>
    /// Secondary test message class for multi-producer tests.
    /// </summary>
    public class ProducerTestMessage2
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public string Value { get; init; } = string.Empty;
    }

    public class ProducerTestMessage2Descendant : ProducerTestMessage2
    {
        public string Extra { get; init; } = string.Empty;
    }

    #endregion
}