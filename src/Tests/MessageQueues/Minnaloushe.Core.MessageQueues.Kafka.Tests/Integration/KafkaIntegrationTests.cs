using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Minnaloushe.Core.ClientProviders.Kafka;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Extensions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Kafka.Extensions;
using Minnaloushe.Core.MessageQueues.Kafka.Consumers.Extensions;
using Minnaloushe.Core.MessageQueues.Kafka.Producers.Extensions;
using Minnaloushe.Core.MessageQueues.Routines;
using Minnaloushe.Core.Tests.Helpers;
using Minnaloushe.Core.Toolbox.AsyncInitializer.Extensions;
using Minnaloushe.Core.Toolbox.JsonConfiguration;
using Minnaloushe.Core.Toolbox.TestHelpers;

namespace Minnaloushe.Core.MessageQueues.Kafka.Tests.Integration;

/// <summary>
/// Integration tests for Kafka producer and consumer working together.
/// These tests verify the end-to-end message flow using a real Kafka container.
/// </summary>
[TestFixture]
[Category("TestContainers")]
[Category("Integration")]
public class KafkaIntegrationTests
{
    #region Fixture members

    #region Fields

    private TestHost _testHost = null!;
    private readonly string _connectionName = Helpers.UniqueString("kafka-connection");
    private readonly string _consumerName = Helpers.UniqueString("test-consumer");
    private readonly string _serviceKey = Helpers.UniqueString("test-topic");
    public static readonly AsyncThresholdCollection<MessageEnvelop<TestMessage>> ReceivedMessages = new();

    #endregion

    #region Properties

    private object AppSettings => MqHelpers.CreateAppSettings(
        [
            MqHelpers.CreateConnection(
                _connectionName,
                type: "kafka", connectionString: GlobalFixture.Kafka1.Instance.GetBootstrapAddress(), serviceKey: _serviceKey)
        ],
        [
            MqHelpers.CreateConsumer(
                _consumerName,
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

                services.ConfigureAsyncInitializers();

                services.AddMessageQueues(configuration)
                    .AddKafkaClientProviders()
                    .AddKafkaConsumers()
                    .AddConsumer<TestMessage, TestIntegrationConsumer>(_consumerName)
                    .AddKafkaProducers()
                    .AddProducer<TestMessage>(_connectionName, producerOptions: new ProducerOptions<TestMessage>()
                    {
                        KeySelector = m => m.Id.ToString("N")
                    })
                    .AddProducer<TestTopicCreateMessage>(_connectionName)
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
    }

    #endregion

    [Test]
    public async Task PublishAsync_WhenMessageIsPublished_ThenShouldBeConsumed()
    {
        // Arrange
        var producer = _testHost.Services.GetRequiredService<IProducer<TestMessage>>();
        var testMessage = new TestMessage { Data = $"Test-{Guid.NewGuid()}" };

        // Act
        await producer.PublishAsync(testMessage, cancellationToken: CancellationToken.None);

        // Wait for message to be consumed
        await ReceivedMessages.WaitUntilCountAtLeastAsync(1, TimeSpan.FromSeconds(10));

        // Assert
        var snapshot = ReceivedMessages.GetSnapshot();

        using var scope = new AssertionScope();

        snapshot.Should().NotBeEmpty();
        snapshot.Should().Contain(m => m.Message.Data == testMessage.Data);
    }

    [Test]
    public async Task PublishAsync_WhenPublishedMultipleMessages_ThenShouldConsumeAll()
    {
        // Arrange
        var producer = _testHost.Services.GetRequiredService<IProducer<TestMessage>>();
        var messages = Enumerable.Range(1, 5)
            .Select(i => new TestMessage { Data = $"Message-{i}-{Guid.NewGuid()}" })
            .ToList();

        // Act
        foreach (var message in messages)
        {
            await producer.PublishAsync(message, cancellationToken: CancellationToken.None);
        }

        // Wait for all messages to be consumed
        await ReceivedMessages.WaitUntilCountAtLeastAsync(5, TimeSpan.FromSeconds(15));
        var snapshot = ReceivedMessages.GetSnapshot();

        // Assert
        snapshot.Count.Should().BeGreaterThanOrEqualTo(messages.Count);

        using var scope = new AssertionScope();

        foreach (var message in messages)
        {
            snapshot.Should().Contain(m => m.Message.Data == message.Data);
        }
    }

    [Test]
    public async Task PublishAsync_WhenPublishedWithHeaders_ThenShouldPassHeadersToConsumer()
    {
        // Arrange
        var producer = _testHost.Services.GetRequiredService<IProducer<TestMessage>>();
        var testMessage = new TestMessage { Data = $"HeaderTest-{Guid.NewGuid()}" };
        var headers = new Dictionary<string, string>
        {
            ["custom-header"] = "custom-value",
            ["correlation-id"] = Guid.NewGuid().ToString()
        };

        // Act
        await producer.PublishAsync(testMessage, null, headers, CancellationToken.None);

        // Wait for message to be consumed
        await ReceivedMessages.WaitUntilCountAtLeastAsync(1, TimeSpan.FromSeconds(10));

        // Assert
        var snapshot = ReceivedMessages.GetSnapshot();

        using var scope = new AssertionScope();

        snapshot.Should().Contain(m => m.Message.Data == testMessage.Data);
        var actualHeaders = snapshot.First().Headers;
        actualHeaders.Should().NotBeNull();
        actualHeaders.Should().BeEquivalentTo(headers);
    }

    [Test]
    public async Task PublishAsync_WhenPublishWithoutKey_ThenShouldPassKeyFromKeySelectorToConsumer()
    {
        // Arrange
        var producer = _testHost.Services.GetRequiredService<IProducer<TestMessage>>();
        var testMessage = new TestMessage { Data = $"KeyTest-{Guid.NewGuid()}" };

        // Act
        await producer.PublishAsync(testMessage);

        // Wait for message to be consumed
        await ReceivedMessages.WaitUntilCountAtLeastAsync(1, TimeSpan.FromSeconds(10));

        // Assert
        ReceivedMessages.GetSnapshot().Should().Contain(m => m.Key == testMessage.Id.ToString("N") && m.Message.Data == testMessage.Data);
    }

    [Test]
    public async Task PublishAsync_WhenPublishWithKey_ThenShouldPassKeyToConsumer()
    {
        // Arrange
        var producer = _testHost.Services.GetRequiredService<IProducer<TestMessage>>();
        var testMessage = new TestMessage { Data = $"KeyTest-{Guid.NewGuid()}" };
        var messageKey = $"test-key-{Guid.NewGuid()}";

        // Act
        await producer.PublishAsync(testMessage, messageKey);

        // Wait for message to be consumed
        await ReceivedMessages.WaitUntilCountAtLeastAsync(1, TimeSpan.FromSeconds(10));

        // Assert
        ReceivedMessages.GetSnapshot().Should().Contain(m => m.Key == messageKey && m.Message.Data == testMessage.Data);

    }

    [Test]
    public async Task PublishAsync_WhenPublishWithKeyAndNullMessage_ThenShouldNotPassNullMessageToConsumer()
    {
        // Arrange
        var producer = _testHost.Services.GetRequiredService<IProducer<TestMessage>>();
        var testMessage = new TestMessage { Data = $"KeyTest-{Guid.NewGuid()}" };
        var messageKey = testMessage.Id.ToString("N");

        // Act
        await producer.PublishAsync(null, messageKey);
        await producer.PublishAsync(testMessage, messageKey);

        // Wait for message to be consumed
        await ReceivedMessages.WaitUntilCountAtLeastAsync(1, TimeSpan.FromSeconds(10));

        // Assert
        ReceivedMessages
            .GetSnapshot()
            .Should().Contain(m => m.Key == messageKey && m.Message.Data == testMessage.Data);

    }

    [Test]
    public void ProducerAndConsumer_WhenRegistered_ThenShouldBeResolvable()
    {
        var scopeFactory = _testHost.Services.GetRequiredService<IServiceScopeFactory>();
        using var serviceScope = scopeFactory.CreateScope();

        // Arrange & Act
        var producer = serviceScope.ServiceProvider.GetService<IProducer<TestMessage>>();
        var consumer = serviceScope.ServiceProvider.GetService<IConsumer<TestMessage>>();

        // Assert

        using var scope = new AssertionScope();

        producer.Should().NotBeNull();
        consumer.Should().NotBeNull();
    }

    [Test]
    public async Task Producer_WhenCalledBeforeConsumer_ThenShouldCreateTopic()
    {
        var admin = _testHost.Services.GetRequiredKeyedService<IKafkaAdminClientProvider>(_connectionName);
        using var lease = admin.Acquire();

        var topicsInitial = lease.Client.Client.GetMetadata(TimeSpan.FromSeconds(2));

        topicsInitial.Topics.Should().NotContain(t => t.Topic == MqNaming.GetSafeName<TestTopicCreateMessage>(),
            "Topic before first publish should not exist");

        var producer = _testHost.Services.GetRequiredService<IProducer<TestTopicCreateMessage>>();

        await producer.PublishAsync(new TestTopicCreateMessage());

        var topic = lease.Client.Client.GetMetadata(TimeSpan.FromSeconds(2));

        topic.Topics.Should().Contain(t => t.Topic == MqNaming.GetSafeName<TestTopicCreateMessage>()
            , "Topic should be created when first message is published");
    }

    #region Helper classes

    /// <summary>
    /// Test message class for integration tests.
    /// </summary>
    public class TestMessage
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public string Data { get; init; } = string.Empty;
    }

    public record TestTopicCreateMessage
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public string Data { get; init; } = string.Empty;
    }
    /// <summary>
    /// Test consumer that collects received messages for verification.
    /// </summary>
    public class TestIntegrationConsumer(ILogger<TestIntegrationConsumer> logger) : IConsumer<TestMessage>
    {
        public Task<bool> HandleMessageAsync(MessageEnvelop<TestMessage> envelop, CancellationToken cancellationToken = default)
        {
#pragma warning disable CA1873
            logger.LogInformation("Received message: {Data}", envelop.Message.Data);
#pragma warning restore CA1873
            KafkaIntegrationTests.ReceivedMessages.Add(envelop);
            return Task.FromResult(true);
        }
    }

    #endregion


}

