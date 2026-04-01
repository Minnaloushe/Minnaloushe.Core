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
public class KafkaProducerIntegrationTests
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
                    .AddProducer<ProducerTestMessage>(_connectionName, _producer1Name)
                    .AddProducer<ProducerTestMessage2>(_connectionName, _producer2Name)
                    .Build();
            },
            beforeStart: async (host) =>
            {
                await host.InvokeAsyncInitializers();
            },
            startHost: true
        );

        await Task.Delay(3000);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _testHost.DisposeAsync();
    }

    #endregion

    [Test]
    public void Producer_WhenRegistered_ThenShouldBeResolvable()
    {
        // Act
        var producer = _testHost.Services.GetService<IProducer<ProducerTestMessage>>();

        // Assert
        producer.Should().NotBeNull("Producer should be registered in the service collection");
    }

    [Test]
    public void Producers_WhenMultipleRegistered_ThenShouldBeSeparateInstances()
    {
        // Arrange & Act
        var producer1 = _testHost.Services.GetKeyedService<IProducer<ProducerTestMessage>>(_producer1Name);
        var producer2 = _testHost.Services.GetKeyedService<IProducer<ProducerTestMessage2>>(_producer2Name);

        // Assert
        using var scope = new AssertionScope();

        producer1.Should().NotBeNull("First producer should be registered with key");
        producer2.Should().NotBeNull("Second producer should be registered with key");
        producer1.Should().NotBeSameAs(producer2, "Different producers should be separate instances");
    }

    [Test]
    public void Producer_WhenRegistered_ThenShouldBeResolvableByConnectionName()
    {
        // Arrange & Act
        var producerByName = _testHost.Services.GetKeyedService<IProducer<ProducerTestMessage>>(_producer1Name);
        var producerByConnection = _testHost.Services.GetKeyedService<IProducer<ProducerTestMessage>>(_connectionName);

        // Assert
        using var scope = new AssertionScope();

        producerByName.Should().NotBeNull("Producer should be resolvable by producer name");
        producerByConnection.Should().NotBeNull("Producer should be resolvable by connection name");
        producerByConnection.Should().BeSameAs(producerByName, "Both resolutions should return the same instance");
    }

    [Test]
    public async Task PublishAsync_WhenMessagePublished_ThenShouldSucceed()
    {
        // Arrange
        var producer = _testHost.Services.GetRequiredService<IProducer<ProducerTestMessage>>();
        var message = new ProducerTestMessage { Content = $"Test-{Guid.NewGuid()}" };

        // Act & Assert - should not throw
        await producer.PublishAsync(message, cancellationToken: CancellationToken.None);
    }

    [Test]
    public async Task PublishAsync_WhenMultipleMessagesPublished_ThenShouldSucceed()
    {
        // Arrange
        var producer = _testHost.Services.GetRequiredService<IProducer<ProducerTestMessage>>();
        var messages = Enumerable.Range(1, 10)
            .Select(i => new ProducerTestMessage { Content = $"BatchMessage-{i}-{Guid.NewGuid()}" })
            .ToList();

        // Act & Assert - all publishes should succeed without throwing
        foreach (var message in messages)
        {
            await producer.PublishAsync(message, cancellationToken: CancellationToken.None);
        }
    }

    [Test]
    public async Task PublishAsync_WhenPublishWithHeaders_ThenShouldSucceed()
    {
        // Arrange
        var producer = _testHost.Services.GetRequiredService<IProducer<ProducerTestMessage>>();
        var message = new ProducerTestMessage { Content = $"HeaderTest-{Guid.NewGuid()}" };
        var headers = new Dictionary<string, string>
        {
            ["x-correlation-id"] = Guid.NewGuid().ToString(),
            ["x-request-id"] = Guid.NewGuid().ToString(),
            ["x-custom-header"] = "custom-value"
        };

        // Act & Assert - should not throw
        await producer.PublishAsync(message, null, headers, CancellationToken.None);
    }

    [Test]
    public async Task PublishAsync_WhenMessagePublished_ThenShouldBeConsumedByRawConsumer()
    {
        // Arrange
        var producer = _testHost.Services.GetRequiredService<IProducer<ProducerTestMessage>>();
        var messageData = $"RawConsumerTest-{Guid.NewGuid()}";
        var message = new ProducerTestMessage { Content = messageData };

        // Create a raw Kafka consumer to verify the message was published
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

        // Assert - verify with raw consumer
        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();

        var topicName = MqNaming.GetSafeName<ProducerTestMessage>(); // Topic name generated from message type
        consumer.Subscribe(topicName);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        ConsumeResult<string, string>? consumeResult = null;

        while (!cts.IsCancellationRequested)
        {
            try
            {
                consumeResult = consumer.Consume(cts.Token);
                if (consumeResult?.Message?.Value?.Contains(messageData) == true)
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
        consumeResult.Message.Value.Should().Contain(messageData, "Consumed message should contain expected data");
    }

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

    #endregion

}

