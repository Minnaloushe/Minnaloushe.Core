using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Minnaloushe.Core.ClientProviders.Kafka.Wrappers;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Extensions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Kafka.Extensions;
using Minnaloushe.Core.MessageQueues.Kafka.Consumers.Extensions;
using Minnaloushe.Core.MessageQueues.Routines;
using Minnaloushe.Core.ServiceDiscovery.Abstractions;
using Minnaloushe.Core.Tests.Helpers;
using Minnaloushe.Core.Toolbox.AsyncInitializer.Extensions;
using Minnaloushe.Core.Toolbox.JsonConfiguration;
using Minnaloushe.Core.Toolbox.TestHelpers;
using Moq;
using System.Text;
using System.Text.Json;

namespace Minnaloushe.Core.MessageQueues.Kafka.Tests.Integration;

/// <summary>
/// Integration tests for Kafka consumer functionality.
/// Tests consumer registration, message handling, and hosted service behavior.
/// </summary>
[TestFixture]
[Category("TestContainers")]
[Category("Integration")]
public class KafkaConsumerIntegrationTests
{
    #region Fixture members

    #region Fields

    private TestHost _testHost = null!;
    public static readonly AsyncThresholdCollection<ConsumerTestMessage> ReceivedMessages = new();
    public static readonly AsyncThresholdCollection<ConsumerTestMessage> ReceivedMessages2 = new();
    private readonly Mock<IServiceDiscoveryService> _serviceDiscoveryMock = new();
    private readonly string _connectionName = Helpers.UniqueString("kafka-consumer-connection");
    private readonly string _serviceKey = Helpers.UniqueString("consumer-test");
    private readonly string _consumerName1 = Helpers.UniqueString("test-consumer1");
    private readonly string _consumerName2 = Helpers.UniqueString("test-consumer2");

    #endregion

    #region Properties

    private object AppSettings =>
        MqHelpers.CreateAppSettings(
            [
                MqHelpers.CreateConnection(
                    _connectionName,
                    type: "kafka",
                    connectionString: GlobalFixture.Kafka1.Instance.GetBootstrapAddress(),
                    serviceKey: _serviceKey,
                    username: GlobalFixture.Kafka1.Username, password: GlobalFixture.Kafka1.Password)
            ],
            [
                MqHelpers.CreateConsumer(
                    _consumerName1,
                    _connectionName),
                MqHelpers.CreateConsumer(
                    _consumerName2,
                    _connectionName
                )
            ]);

    #endregion

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {

        ReceivedMessages.Clear();

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

                services.AddSingleton(_serviceDiscoveryMock.Object);

                services.ConfigureAsyncInitializers();

                services.AddMessageQueues(configuration)
                    .AddKafkaClientProviders()
                    .AddKafkaConsumers()
                    .AddConsumer<ConsumerTestMessage, TestConsumerHandler>(_consumerName1)
                    .AddConsumer<ConsumerTestMessage2, TestConsumerHandler2>(_consumerName2)
                    .Build();
            },
            beforeStart: async (host) =>
            {
                await host.InvokeAsyncInitializers();
            },
            startHost: true
        );

        // Give time for consumer initialization and topic creation
        await Task.Delay(5000);
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
        ReceivedMessages2.Clear();
    }

    #region Helper methods

    private static IProducer<string, string> CreateProducer()
    {
        var producerConfig = new ProducerConfig
        {
            BootstrapServers = GlobalFixture.Kafka1.Instance.GetBootstrapAddress(),
            SecurityProtocol = SecurityProtocol.Plaintext,
            SaslMechanism = SaslMechanism.Plain,
            SaslUsername = GlobalFixture.Kafka1.Username,
            SaslPassword = GlobalFixture.Kafka1.Password
        };

        return new ProducerBuilder<string, string>(producerConfig).Build();
    }

    private static Task<DeliveryResult<string, string>> SendAsync<T>(IProducer<string, string> producer, T message)
    {
        var topicName = MqNaming.GetSafeName<T>();
        var messageJson = JsonSerializer.Serialize(message);

        return ProduceWithTimeoutAsync(producer, topicName, new Message<string, string>
        {
            Key = Guid.NewGuid().ToString(),
            Value = messageJson
        });
    }

    // Helper to publish a message using a raw Kafka producer. Keeps existing tests DRY.
    private static async Task PublishRawAsync<T>(T message, Headers? headers = null)
    {
        var topicName = MqNaming.GetSafeName<T>();
        var messageJson = JsonSerializer.Serialize(message);

        using var producer = CreateProducer();
        await ProduceWithTimeoutAsync(producer, topicName, new Message<string, string>
        {
            Key = Guid.NewGuid().ToString(),
            Value = messageJson,
            Headers = headers
        });
    }

    private static async Task PublishRawBatchAsync<T>(IEnumerable<T> messages)
    {
        var topicName = MqNaming.GetSafeName<T>();

        using var producer = CreateProducer();

        foreach (var message in messages)
        {
            var messageJson = JsonSerializer.Serialize(message);

            await ProduceWithTimeoutAsync(producer, topicName, new Message<string, string>
            {
                Key = Guid.NewGuid().ToString(),
                Value = messageJson
            });
        }
    }

    private static async Task<DeliveryResult<string, string>> ProduceWithTimeoutAsync(
        IProducer<string, string> producer,
        string topic,
        Message<string, string> message,
        TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(10);

        var produceTask = producer.ProduceAsync(topic, message);
        var delayTask = Task.Delay(timeout.Value);

        var completed = await Task.WhenAny(produceTask, delayTask);

        if (completed != produceTask)
        {
            throw new TimeoutException($"ProduceAsync did not complete within {timeout}");
        }

        return await produceTask; // already completed
    }

    #endregion

    #endregion

    [Test]
    public void Consumer_WhenRegistered_ThenShouldBeResolvable()
    {
        var scopeFactory = _testHost.Services.GetRequiredService<IServiceScopeFactory>();
        using var serviceScope = scopeFactory.CreateScope();

        // Act
        var consumer = serviceScope.ServiceProvider.GetService<IConsumer<ConsumerTestMessage>>();
        var consumer2 = serviceScope.ServiceProvider.GetService<IConsumer<ConsumerTestMessage2>>();

        // Assert
        consumer.Should().NotBeNull("Consumer should be registered in the service collection");
        consumer2.Should().NotBeNull("Consumer2 should be registered in the service collection");
    }

    [Test]
    public void ConsumerHostedService_WhenRegistered_ThenShouldBeResolvable()
    {
        // Act
        var hostedServices = _testHost.Services.GetServices<IHostedService>().ToList();

        // Assert
        hostedServices.Should().NotBeEmpty("Hosted services should be registered");
        hostedServices.OfType<ConsumerHostedService<ConsumerTestMessage?, IKafkaConsumerClientWrapper>>()
            .Should().NotBeEmpty("Consumer hosted service should be registered");
    }

    [Test]
    public void ConsumerInitializer_WhenRegisteredWithKey_ThenShouldBeResolvable()
    {
        // Act
        var initializer = _testHost.Services.GetKeyedService<IConsumerInitializer>(_consumerName1);

        // Assert
        initializer.Should().NotBeNull("Consumer initializer should be registered with consumer name as key");
    }

    [Test]
    public async Task PublishRawAsync_WhenMessagePublished_ThenShouldReceiveMessage()
    {
        // Arrange
        // Use the same topic naming convention as the consumer

        var messageContent = $"RawProducerTest-{Guid.NewGuid()}";
        var message = new ConsumerTestMessage { Content = messageContent };

        // Act - publish using raw Kafka producer
        await PublishRawAsync(message);

        // Assert - wait for consumer to receive the message
        var received = await ReceivedMessages.WaitUntilCountAtLeastAsync(1, TimeSpan.FromSeconds(15));

        received.Should().BeTrue("Consumer should receive the message");
        var snapshot = ReceivedMessages.GetSnapshot();
        snapshot.Should().Contain(m => m.Content == messageContent);
    }

    [Test]
    public async Task PublishRawBatchAsync_WhenMultipleMessagesPublished_ThenShouldReceiveAllMessages()
    {
        // Arrange
        var messages = Enumerable.Range(1, 5)
            .Select(i => new ConsumerTestMessage { Content = $"BatchMessage-{i}-{Guid.NewGuid()}" })
            .ToList();

        // Act - publish using raw Kafka producer
        await PublishRawBatchAsync(messages);

        // Assert - wait for consumer to receive all messages
        var received = await ReceivedMessages.WaitUntilCountAtLeastAsync(5, TimeSpan.FromSeconds(20));

        received.Should().BeTrue("Consumer should receive all messages");
        var snapshot = ReceivedMessages.GetSnapshot();
        snapshot.Count.Should().BeGreaterThanOrEqualTo(5);

        foreach (var message in messages)
        {
            snapshot.Should().Contain(m => m.Content == message.Content);
        }
    }

    [Test]
    public async Task SendAsync_WhenMessagesPublishedToBoth_ThenShouldReceiveMessages()
    {
        var topicName = MqNaming.GetSafeName<ConsumerTestMessage>();
        var topicName2 = MqNaming.GetSafeName<ConsumerTestMessage2>();

        using var producer = CreateProducer();

        var result = await SendAsync(producer, new ConsumerTestMessage() { Content = topicName });
        var result2 = await SendAsync(producer, new ConsumerTestMessage2() { Content = topicName2 });

        // Wait for both consumers to receive their respective messages
        var received1 = await ReceivedMessages.WaitUntilCountAtLeastAsync(1, TimeSpan.FromSeconds(10));
        var received2 = await ReceivedMessages2.WaitUntilCountAtLeastAsync(1, TimeSpan.FromSeconds(10));

        using var scope = new AssertionScope();

        received1.Should().BeTrue("Consumer1 should receive the message");
        received2.Should().BeTrue("Consumer2 should receive the message");
    }

    [Test]
    public async Task PublishRawAsync_WhenMessageWithHeadersPublished_ThenShouldReceiveMessageWithHeaders()
    {
        // Arrange
        var messageContent = $"HeaderTest-{Guid.NewGuid()}";
        var message = new ConsumerTestMessage { Content = messageContent };

        var headers = new Headers
        {
            { "x-correlation-id", Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()) },
            { "x-custom-header", Encoding.UTF8.GetBytes("custom-value") }
        };

        // Act - publish using raw Kafka producer with headers
        await PublishRawAsync(message, headers);

        // Assert - wait for consumer to receive the message
        var received = await ReceivedMessages.WaitUntilCountAtLeastAsync(1, TimeSpan.FromSeconds(15));

        received.Should().BeTrue("Consumer should receive the message with headers");
        var snapshot = ReceivedMessages.GetSnapshot();
        snapshot.Should().Contain(m => m.Content == messageContent);
    }

    #region Helper classes

    /// <summary>
    /// Test message class for consumer tests.
    /// </summary>
    public class ConsumerTestMessage
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public string Content { get; init; } = string.Empty;
    }

    public class ConsumerTestMessage2
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public string Content { get; init; } = string.Empty;
    }

    /// <summary>
    /// Test consumer handler that collects received messages for verification.
    /// </summary>
    public class TestConsumerHandler(ILogger<TestConsumerHandler> logger) : IConsumer<ConsumerTestMessage>
    {
        public Task<bool> HandleMessageAsync(MessageEnvelop<ConsumerTestMessage> envelop, CancellationToken cancellationToken = default)
        {
#pragma warning disable CA1873
            logger.LogInformation("Consumer received message: {Content}", envelop.Message.Content);
#pragma warning restore CA1873
            KafkaConsumerIntegrationTests.ReceivedMessages.Add(envelop.Message);
            return Task.FromResult(true);
        }
    }

    public class TestConsumerHandler2(ILogger<TestConsumerHandler> logger) : IConsumer<ConsumerTestMessage2>
    {
        public Task<bool> HandleMessageAsync(MessageEnvelop<ConsumerTestMessage2> envelop, CancellationToken cancellationToken = default)
        {
#pragma warning disable CA1873
            logger.LogInformation("Consumer received message: {Content}", envelop.Message.Content);
#pragma warning restore CA1873
            KafkaConsumerIntegrationTests.ReceivedMessages2.Add(new ConsumerTestMessage { Content = envelop.Message.Content });
            return Task.FromResult(true);
        }
    }

    #endregion

}



