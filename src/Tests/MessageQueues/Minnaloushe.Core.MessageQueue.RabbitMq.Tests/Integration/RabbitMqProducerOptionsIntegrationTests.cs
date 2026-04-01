using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Extensions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.RabbitMq.Extensions;
using Minnaloushe.Core.MessageQueues.RabbitMq.Producers;
using Minnaloushe.Core.MessageQueues.Routines;
using Minnaloushe.Core.Tests.Helpers;
using Minnaloushe.Core.Toolbox.AsyncInitializer.Extensions;
using Minnaloushe.Core.Toolbox.JsonConfiguration;
using Minnaloushe.Core.Toolbox.TestHelpers;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace Minnaloushe.Core.MessageQueue.RabbitMq.Tests.Integration;

/// <summary>
/// Integration tests for RabbitMQ producer options functionality.
/// Tests runtime exchange resolution without key selector (not supported in RabbitMQ).
/// </summary>
[TestFixture]
[Category("TestContainers")]
[Category("Integration")]
public class RabbitMqProducerOptionsIntegrationTests
{
    #region Fixture members

    #region Fields

    private TestHost _testHost = null!;
    private readonly string _serviceKey = Helpers.UniqueString("test-topic");
    private readonly string _connectionName = Helpers.UniqueString("rabbit-producer-connection");
    private readonly string _producer1Name = Helpers.UniqueString("producer-1");
    private readonly string _producer2Name = Helpers.UniqueString("producer-2");

    #endregion

    #region Properties

    private object AppSettings => MqHelpers.CreateAppSettings(
        [
            MqHelpers.CreateConnection(
                _connectionName,
                type: "rabbit", container: GlobalFixture.RabbitMqInstance1, serviceKey: _serviceKey)
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
                    .AddRabbitMqClientProviders()
                    .AddRabbitMqProducers()
                    .AddProducer<ProducerTestMessage>(_connectionName, _producer1Name)
                    .AddProducer<ProducerTestMessage2>(_connectionName, _producer2Name, new ProducerOptions<ProducerTestMessage2>()
                    {
                        ResolveMessageTypeAtRuntime = true
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

        var runtimeExchangeName = MqNaming.GetSafeName<ProducerTestMessage2Descendant>();

        // Ensure the runtime exchange exists before publishing
        await RabbitMqHelpers.EnsureExchangeExistsAsync(GlobalFixture.RabbitMqInstance1, runtimeExchangeName);

        var queueName = $"test-queue-{Guid.NewGuid()}";

        await using var channel =
            await RabbitMqHelpers.CreateAndBindQueueAsync(GlobalFixture.RabbitMqInstance1, runtimeExchangeName, queueName);

        var consumer = new AsyncEventingBasicConsumer(channel);
        var tcs = new TaskCompletionSource<string>();

        consumer.ReceivedAsync += (sender, ea) =>
        {
            var body = ea.Body.ToArray();
            var messageBody = Encoding.UTF8.GetString(body);
            tcs.TrySetResult(messageBody);
            return Task.CompletedTask;
        };

        await channel.BasicConsumeAsync(queueName, autoAck: true, consumer: consumer);

        // Act - NOW publish the message after consumer is ready
        await producer.PublishAsync(message, cancellationToken: CancellationToken.None);

        // Assert
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        string? consumedMessage = null;

        try
        {
            consumedMessage = await tcs.Task.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
        }

        using var scope = new AssertionScope();

        consumedMessage.Should().NotBeNull("Message should be consumed from runtime-typed exchange");
        consumedMessage.Should().Contain(message.Value, "Message should contain expected value");
        consumedMessage.Should().Contain(message.Extra, "Message should contain descendant-specific data");
    }

    [Test]
    public async Task PublishAsync_WhenRuntimeTypeResolutionEnabled_ThenShouldNotPublishToStaticTypeExchange()
    {
        // Arrange
        var producer = _testHost.Services.GetRequiredKeyedService<IProducer<ProducerTestMessage2>>(_producer2Name);
        var uniqueMarker = $"StaticTypeTest-{Guid.NewGuid()}";
        var staticExchangeName = MqNaming.GetSafeName<ProducerTestMessage2>();

        // First, ensure the static exchange exists by pre-creating it
        await RabbitMqHelpers.EnsureExchangeExistsAsync(GlobalFixture.RabbitMqInstance1, staticExchangeName);

        // Publish a base type message to the static exchange
        var baseMessage = new ProducerTestMessage2
        {
            Id = Guid.NewGuid(),
            Value = $"BaseMessage-{Guid.NewGuid()}"
        };
        await producer.PublishAsync(baseMessage, cancellationToken: CancellationToken.None);

        // Wait for exchange to be ready
        await RabbitMqHelpers.WaitForExchangeCreation(GlobalFixture.RabbitMqInstance1, staticExchangeName);

        // Now publish the descendant message
        var message = new ProducerTestMessage2Descendant
        {
            Id = Guid.NewGuid(),
            Value = uniqueMarker,
            Extra = "ExtraData"
        };

        var runtimeExchangeName = MqNaming.GetSafeName<ProducerTestMessage2Descendant>();
        await RabbitMqHelpers.EnsureExchangeExistsAsync(GlobalFixture.RabbitMqInstance1, runtimeExchangeName);

        // Act
        await producer.PublishAsync(message, cancellationToken: CancellationToken.None);
        await Task.Delay(1000);

        // Assert
        var queueName = $"test-queue-{Guid.NewGuid()}";

        await using var channel =
            await RabbitMqHelpers.CreateAndBindQueueAsync(GlobalFixture.RabbitMqInstance1, staticExchangeName,
                queueName);

        var consumer = new AsyncEventingBasicConsumer(channel);
        var foundInStaticExchange = false;

        consumer.ReceivedAsync += (sender, ea) =>
        {
            var body = ea.Body.ToArray();
            var messageBody = Encoding.UTF8.GetString(body);
            if (messageBody.Contains(uniqueMarker))
            {
                foundInStaticExchange = true;
            }
            return Task.CompletedTask;
        };

        await channel.BasicConsumeAsync(queueName, autoAck: true, consumer: consumer);

        await Task.Delay(TimeSpan.FromSeconds(5));

        foundInStaticExchange.Should().BeFalse(
            "Message should not be found in static type exchange when runtime type resolution is enabled");
    }

    [Test]
    public async Task PublishAsync_WhenRuntimeTypeResolutionEnabled_WithBaseType_ThenShouldUseBaseTypeExchange()
    {
        // Arrange
        var producer = _testHost.Services.GetRequiredKeyedService<IProducer<ProducerTestMessage2>>(_producer2Name);
        var message = new ProducerTestMessage2
        {
            Id = Guid.NewGuid(),
            Value = $"BaseTypeTest-{Guid.NewGuid()}"
        };

        var baseExchangeName = MqNaming.GetSafeName<ProducerTestMessage2>();

        // Ensure the base exchange exists before publishing
        await RabbitMqHelpers.EnsureExchangeExistsAsync(GlobalFixture.RabbitMqInstance1, baseExchangeName);

        // Setup consumer BEFORE publishing

        var queueName = $"test-queue-{Guid.NewGuid()}";

        await using var channel =
            await RabbitMqHelpers.CreateAndBindQueueAsync(GlobalFixture.RabbitMqInstance1, baseExchangeName, queueName);

        var consumer = new AsyncEventingBasicConsumer(channel);
        var tcs = new TaskCompletionSource<string>();

        consumer.ReceivedAsync += (sender, ea) =>
        {
            var body = ea.Body.ToArray();
            var messageBody = Encoding.UTF8.GetString(body);
            tcs.TrySetResult(messageBody);
            return Task.CompletedTask;
        };

        await channel.BasicConsumeAsync(queueName, autoAck: true, consumer: consumer);

        // Act - NOW publish the message after consumer is ready
        await producer.PublishAsync(message, cancellationToken: CancellationToken.None);

        // Assert
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        string? consumedMessage = null;

        try
        {
            consumedMessage = await tcs.Task.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
        }

        using var scope = new AssertionScope();

        consumedMessage.Should().NotBeNull(
            "When publishing base type with runtime resolution, message should go to base type exchange");
        consumedMessage.Should().Contain(message.Value, "Message should contain expected value");
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