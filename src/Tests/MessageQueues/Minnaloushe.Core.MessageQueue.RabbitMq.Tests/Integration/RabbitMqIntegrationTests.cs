using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Minnaloushe.Core.MessageQueue.RabbitMq.Tests.TestClasses;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Extensions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.RabbitMq.Extensions;
using Minnaloushe.Core.MessageQueues.RabbitMq.Producers;
using Minnaloushe.Core.Tests.Helpers;
using Minnaloushe.Core.Toolbox.AsyncInitializer.Extensions;
using Minnaloushe.Core.Toolbox.TestHelpers;

namespace Minnaloushe.Core.MessageQueue.RabbitMq.Tests.Integration;

[TestFixture]
[Category("TestContainers")]
[Category("Integration")]
public class RabbitMqIntegrationTests
{
    #region Fixture members

    #region Constants

    private const int MessageDelayMs = 2000;
    private const int WaitTimeoutSeconds = 5;

    #endregion

    #region Fields

    private TestHost _testHost = null!;
    private readonly string _connectionName = Helpers.UniqueString("rabbit-connection");
    private readonly string _consumerName = Helpers.UniqueString("test-consumer");
    private readonly string _serviceKey = Helpers.UniqueString("test-queue");
    public static readonly AsyncThresholdCollection<TestMessage> ReceivedMessages = new();

    #endregion

    #region Properties

    private object AppSettings =>
        MqHelpers.CreateAppSettings(
            [
                MqHelpers.CreateConnection(_connectionName, "rabbit", container: GlobalFixture.RabbitMqInstance1)
            ],
            [
                MqHelpers.CreateConsumer(_consumerName, _connectionName)
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

                services.ConfigureAsyncInitializers();

                services.AddMessageQueues(configuration)
                    .AddRabbitMqClientProviders()
                    .AddRabbitMqConsumers()
                    .AddConsumer<TestMessage, TestIntegrationConsumer>(_consumerName)
                    .AddRabbitMqProducers()
                    .AddProducer<TestMessage>(_connectionName)
                    .Build();
            },
            beforeStart: async (host) =>
            {
                await host.InvokeAsyncInitializers();
            },
            startHost: true
        );

        await Task.Delay(MessageDelayMs);
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
    public async Task PublishAsync_WhenMessagePublished_ThenShouldBeConsumed()
    {
        // Arrange
        var producer = _testHost.Services.GetRequiredService<IProducer<TestMessage>>();
        var testMessage = new TestMessage { Data = $"Test-{Guid.NewGuid()}" };

        // Act
        await producer.PublishAsync(testMessage, cancellationToken: CancellationToken.None);
        await ReceivedMessages.WaitUntilCountAtLeastAsync(1, TimeSpan.FromSeconds(WaitTimeoutSeconds));

        // Assert
        var snapshot = ReceivedMessages.GetSnapshot();

        using var scope = new AssertionScope();

        snapshot.Should().NotBeEmpty();
        snapshot.Should().Contain(m => m.Data == testMessage.Data);
    }

    [Test]
    public async Task PublishAsync_WhenPublishMultipleMessages_ThenShouldConsumeAll()
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
        await ReceivedMessages.WaitUntilCountAtLeastAsync(5, TimeSpan.FromSeconds(WaitTimeoutSeconds));

        // Assert
        var snapshot = ReceivedMessages.GetSnapshot();

        using var scope = new AssertionScope();

        snapshot.Count.Should().BeGreaterThanOrEqualTo(messages.Count);

        foreach (var message in messages)
        {
            snapshot.Should().Contain(m => m.Data == message.Data);
        }
    }

    [Test]
    public async Task PublishAsync_WhenPublishWithHeaders_ThenShouldWork()
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
        await ReceivedMessages.WaitUntilCountAtLeastAsync(1, TimeSpan.FromSeconds(2));

        // Assert
        ReceivedMessages.GetSnapshot().Should().Contain(m => m.Data == testMessage.Data);
    }

    [Test]
    public void ProducerAndConsumer_WhenRegistered_ThenShouldBeResolvable()
    {
        var scopeFactory = _testHost.Services.GetRequiredService<IServiceScopeFactory>();
        using var serviceScope = scopeFactory.CreateScope();

        // Arrange & Act
        var producer = _testHost.Services.GetService<IProducer<TestMessage>>();
        var consumer = serviceScope.ServiceProvider.GetService<IConsumer<TestMessage>>();

        // Assert
        using var scope = new AssertionScope();

        producer.Should().NotBeNull();
        consumer.Should().NotBeNull();
    }
}

#region Helper classes

/// <summary>
/// Test consumer that collects received messages for verification
/// </summary>
public class TestIntegrationConsumer(ILogger<TestIntegrationConsumer> logger) : IConsumer<TestMessage>
{
    public Task<bool> HandleMessageAsync(MessageEnvelop<TestMessage> envelop, CancellationToken cancellationToken = default)
    {
#pragma warning disable CA1873
        logger.LogInformation("Received message: {Data}", envelop.Message.Data);
#pragma warning restore CA1873
        RabbitMqIntegrationTests.ReceivedMessages.Add(envelop.Message);
        return Task.FromResult(true);
    }
}

#endregion
