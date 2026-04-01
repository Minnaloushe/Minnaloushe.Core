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

//TODO Refactor, remove Task.Delay
/// <summary>
/// Integration tests for RabbitMQ producer functionality.
/// Tests producer registration, message publishing, and header handling.
/// </summary>
[TestFixture]
[Category("TestContainers")]
[Category("Integration")]
public class RabbitMqProducerIntegrationTests
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
                    .AddProducer<ProducerTestMessage2>(_connectionName, _producer2Name)
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

        var exchangeName = MqNaming.GetSafeName<ProducerTestMessage>();
        await RabbitMqHelpers.EnsureExchangeExistsAsync(GlobalFixture.RabbitMqInstance1, exchangeName);

        // Act & Assert
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

        var exchangeName = MqNaming.GetSafeName<ProducerTestMessage>();
        await RabbitMqHelpers.EnsureExchangeExistsAsync(GlobalFixture.RabbitMqInstance1, exchangeName);

        // Act & Assert
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

        var exchangeName = MqNaming.GetSafeName<ProducerTestMessage>();
        await RabbitMqHelpers.EnsureExchangeExistsAsync(GlobalFixture.RabbitMqInstance1, exchangeName);

        // Act & Assert
        await producer.PublishAsync(message, null, headers, CancellationToken.None);
    }

    [Test]
    public async Task PublishAsync_WhenMessagePublished_ThenShouldBeConsumedByRawConsumer()
    {
        // Arrange
        var producer = _testHost.Services.GetRequiredService<IProducer<ProducerTestMessage>>();
        var messageData = $"RawConsumerTest-{Guid.NewGuid()}";
        var message = new ProducerTestMessage { Content = messageData };

        var exchangeName = MqNaming.GetSafeName<ProducerTestMessage>();
        await RabbitMqHelpers.EnsureExchangeExistsAsync(GlobalFixture.RabbitMqInstance1, exchangeName);

        var factory = new ConnectionFactory
        {
            HostName = GlobalFixture.RabbitMqInstance1.Host,
            Port = GlobalFixture.RabbitMqInstance1.Port,
            UserName = GlobalFixture.RabbitMqInstance1.Username,
            Password = GlobalFixture.RabbitMqInstance1.Password
        };

        // Setup consumer BEFORE publishing
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        var queueName = $"test-queue-{Guid.NewGuid()}";

        await channel.ExchangeDeclareAsync(exchangeName, ExchangeType.Fanout, durable: true, autoDelete: false);
        await channel.QueueDeclareAsync(queueName, durable: false, exclusive: true, autoDelete: true);
        await channel.QueueBindAsync(queueName, exchangeName, string.Empty);

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

        // Act
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

        consumedMessage.Should().NotBeNull("Message should be consumed from RabbitMQ");
        consumedMessage.Should().Contain(messageData, "Consumed message should contain expected data");
    }

    [Test]
    public async Task PublishAsync_WhenPublishWithHeaders_ThenHeadersShouldBeIncluded()
    {
        // Arrange
        var producer = _testHost.Services.GetRequiredService<IProducer<ProducerTestMessage>>();
        var messageData = $"HeadersTest-{Guid.NewGuid()}";
        var message = new ProducerTestMessage { Content = messageData };
        var correlationId = Guid.NewGuid().ToString();
        var headers = new Dictionary<string, string>
        {
            ["x-correlation-id"] = correlationId,
            ["x-custom-header"] = "test-value"
        };

        var exchangeName = MqNaming.GetSafeName<ProducerTestMessage>();
        await RabbitMqHelpers.EnsureExchangeExistsAsync(GlobalFixture.RabbitMqInstance1, exchangeName);

        var factory = new ConnectionFactory
        {
            HostName = GlobalFixture.RabbitMqInstance1.Host,
            Port = GlobalFixture.RabbitMqInstance1.Port,
            UserName = GlobalFixture.RabbitMqInstance1.Username,
            Password = GlobalFixture.RabbitMqInstance1.Password
        };

        // Setup consumer BEFORE publishing
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        var queueName = $"test-queue-{Guid.NewGuid()}";

        await channel.ExchangeDeclareAsync(exchangeName, ExchangeType.Fanout, durable: true, autoDelete: false);
        await channel.QueueDeclareAsync(queueName, durable: false, exclusive: true, autoDelete: true);
        await channel.QueueBindAsync(queueName, exchangeName, string.Empty);

        var consumer = new AsyncEventingBasicConsumer(channel);
        var tcs = new TaskCompletionSource<BasicDeliverEventArgs>();

        consumer.ReceivedAsync += (sender, ea) =>
        {
            tcs.TrySetResult(ea);
            return Task.CompletedTask;
        };

        await channel.BasicConsumeAsync(queueName, autoAck: true, consumer: consumer);

        // Act
        await producer.PublishAsync(message, null, headers, CancellationToken.None);

        // Assert
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        BasicDeliverEventArgs? eventArgs = null;

        try
        {
            eventArgs = await tcs.Task.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
        }

        using var scope = new AssertionScope();

        eventArgs.Should().NotBeNull("Message should be consumed");
        eventArgs!.BasicProperties.Should().NotBeNull();
        eventArgs.BasicProperties.Headers.Should().NotBeNull();
        eventArgs.BasicProperties.Headers.Should().ContainKey("x-correlation-id");

#pragma warning disable CS8604 // Possible null reference argument.
#pragma warning disable CS8600 // Possible null reference argument.
        var headerValue = Encoding.UTF8.GetString((byte[])eventArgs.BasicProperties.Headers["x-correlation-id"]);
#pragma warning restore CS8600 // Possible null reference argument.
#pragma warning restore CS8604 // Possible null reference argument.

        headerValue.Should().Be(correlationId, "Header value should match");
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