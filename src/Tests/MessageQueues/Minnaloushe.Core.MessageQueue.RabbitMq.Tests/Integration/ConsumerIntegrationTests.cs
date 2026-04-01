using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.Abstractions.ErrorHandling;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Routines;
using Minnaloushe.Core.MessageQueues.RabbitMq.Consumers;
using Minnaloushe.Core.Tests.Helpers;
using Minnaloushe.Core.Toolbox.TestHelpers;
using Moq;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace Minnaloushe.Core.MessageQueue.RabbitMq.Tests.Integration;

/// <summary>
/// Integration tests for RabbitMqMessageEngine with actual RabbitMQ instance.
/// Tests cover message processing, acknowledgment, and error handling strategies.
/// </summary>
[TestFixture]
[Category("Integration")]
[Category("TestContainers")]
public class RabbitMqEngineIntegrationTests
{
    private RabbitMqMessageEngine<TestMessage> _sut = null!;
    private string _serviceKey = string.Empty;
    private string _queueName = string.Empty;
    private string _exchangeName = string.Empty;
    private IConnection _connection = null!;
    private IChannel _channel = null!;
    private Mock<IConsumer<TestMessage>> _mockConsumer = new();
    private Mock<IErrorHandlingStrategy> _mockErrorHandlingStrategy = null!;
    private readonly MessageQueueNamingConventionsProvider _namingConventionsProvider = new();

    private const string ConsumerName = "consumer";
    private const int DefaultTimeoutSeconds = 10;
    private const int DefaultCancellationTimeoutSeconds = 30;

    [SetUp]
    public async Task SetUp()
    {
        _mockConsumer = new Mock<IConsumer<TestMessage?>>()!;
        _mockErrorHandlingStrategy = new Mock<IErrorHandlingStrategy>();
        _serviceKey = Helpers.UniqueString("test-service");

        _exchangeName = _namingConventionsProvider.GetTopicName<TestMessage>();
        _queueName = _namingConventionsProvider.GetServiceKey<TestMessage>(_serviceKey);

        var factory = new ConnectionFactory
        {
            HostName = GlobalFixture.RabbitMqInstance1.Host,
            Port = GlobalFixture.RabbitMqInstance1.Port,
            UserName = GlobalFixture.RabbitMqInstance1.Username,
            Password = GlobalFixture.RabbitMqInstance1.Password
        };

        _connection = await factory.CreateConnectionAsync();
        _channel = await _connection.CreateChannelAsync();

        await _channel.QueueDeclareAsync(_queueName, durable: false, exclusive: false, autoDelete: false);

        SetupDefaultErrorHandling();

        _sut = CreateMessageEngine();
    }

    [TearDown]
    public async Task TearDown()
    {
        await CleanupResources();
    }

    private void SetupDefaultErrorHandling()
    {
        _mockErrorHandlingStrategy
            .Setup(x => x.HandleErrorAsync(It.IsAny<FailedMessageDetails>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ErrorHandlingResult.Discarded);
    }

    private RabbitMqMessageEngine<TestMessage> CreateMessageEngine()
    {
        return new RabbitMqMessageEngine<TestMessage>(
            ConsumerName,
            _connection,
            _mockConsumer.Object,
            new MessageQueueOptions
            {
                ServiceKey = _serviceKey,
                Parameters = new Dictionary<string, JsonElement>()
                {
                    {"RequeueNacked", JsonElement.Parse("true")}
                }
            },
            _mockErrorHandlingStrategy.Object,
            new MessageQueueNamingConventionsProvider(),
            new NullLogger<RabbitMqMessageEngine<TestMessage>>()
        );
    }

    private async Task CleanupResources()
    {
        await _channel.CloseAsync();
        await _channel.DisposeAsync();

        await _connection.CloseAsync();
        await _connection.DisposeAsync();
    }

    private async Task PublishMessageAsync(TestMessage message)
    {
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        var queueName = _namingConventionsProvider.GetServiceKey<TestMessage>(_serviceKey);
        await _channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queueName,
            body: body
        );
    }

    private static CancellationTokenSource CreateDefaultCts(TimeSpan? timeout = null)
    {
        return new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(DefaultCancellationTimeoutSeconds));
    }

    private Task StartRun(CancellationTokenSource cts)
    {
        return _sut.RunAsync(cts.Token, cts.Token);
    }

    private async Task<uint> GetQueueMessageCount()
    {
        return await _channel.MessageCountAsync(_queueName);
    }

    [Test]
    public async Task RunAsync_WhenMessagePublished_ThenShouldConsumeMessage()
    {
        var testMessage = new TestMessage { Id = Guid.NewGuid(), Content = "Test content" };
        var messageReceived = new TaskCompletionSource<TestMessage>();

        _mockConsumer
            .Setup(x => x.HandleMessageAsync(
                It.IsAny<MessageEnvelop<TestMessage>>(),
                It.IsAny<CancellationToken>()))
            .Callback<MessageEnvelop<TestMessage>, CancellationToken>((msg, _) =>
                messageReceived.TrySetResult(msg.Message)
            )
            .ReturnsAsync(true);

        using var cts = CreateDefaultCts();
        var runTask = StartRun(cts);

        await PublishMessageAsync(testMessage);

        var receivedMessage = await messageReceived.WaitAsync(DefaultTimeoutSeconds);

        receivedMessage.Should().NotBeNull();
        receivedMessage.Id.Should().Be(testMessage.Id);
        receivedMessage.Content.Should().Be(testMessage.Content);

        await cts.CancelAsync();
        await runTask;

        _mockConsumer.Verify(x => x.HandleMessageAsync(
            It.Is<MessageEnvelop<TestMessage>>(m => m.Message.Id == testMessage.Id && m.Message.Content == testMessage.Content),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task RunAsync_WhenHandlerReturnsTrue_ThenShouldAckMessage()
    {
        var testMessage = new TestMessage { Id = Guid.NewGuid(), Content = "Success message" };
        var messageReceived = new TaskCompletionSource<bool>();

        _mockConsumer
            .Setup(x => x.HandleMessageAsync(
                It.IsAny<MessageEnvelop<TestMessage>>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => messageReceived.TrySetResult(true))
            .ReturnsAsync(true);

        using var cts = CreateDefaultCts();
        var runTask = StartRun(cts);

        await PublishMessageAsync(testMessage);
        await messageReceived.WaitAsync(DefaultTimeoutSeconds);

        cts.Cancel();
        await runTask;

        var messageCount = await GetQueueMessageCount();
        messageCount.Should().Be(0u, "Message should be acknowledged and removed from queue");
    }

    [Test]
    public async Task RunAsync_WhenHandlerReturnsFalseAndErrorStrategyRequeues_ThenShouldNackMessage()
    {
        var testMessage = new TestMessage { Id = Guid.NewGuid(), Content = "Failed message" };
        var handlerCalls = new AsyncThresholdCollection<bool>();

        _mockErrorHandlingStrategy
            .Setup(x => x.HandleErrorAsync(It.IsAny<FailedMessageDetails>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ErrorHandlingResult.Requeued);

        _mockConsumer
            .Setup(x => x.HandleMessageAsync(
                It.IsAny<MessageEnvelop<TestMessage>>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => handlerCalls.Add(true))
            .ReturnsAsync(false);

        using var cts = CreateDefaultCts();
        var runTask = StartRun(cts);

        await PublishMessageAsync(testMessage);
        var reachedThreshold = await handlerCalls.WaitUntilCountAtLeastAsync(2, TimeSpan.FromSeconds(DefaultTimeoutSeconds), cts.Token);
        reachedThreshold.Should().BeTrue("Message should be reprocessed after nack within timeout");

        await cts.CancelAsync();
        await runTask;

        handlerCalls.Count.Should().BeGreaterThanOrEqualTo(2, "Message should be reprocessed after nack");

        _mockConsumer.Verify(x => x.HandleMessageAsync(
            It.IsAny<MessageEnvelop<TestMessage>>(),
            It.IsAny<CancellationToken>()),
            Times.AtLeast(2), "Handler should be called multiple times due to nack and requeue");

        _mockErrorHandlingStrategy.Verify(x => x.HandleErrorAsync(
            It.IsAny<FailedMessageDetails>(),
            It.IsAny<CancellationToken>()),
            Times.AtLeast(2), "Error handling strategy should be invoked for each failure");
    }

    [Test]
    public async Task RunAsync_WhenHandlerThrowsException_ThenShouldInvokeErrorHandlingStrategy()
    {
        var testMessage = new TestMessage { Id = Guid.NewGuid(), Content = "Exception message" };
        var errorHandlingCompleted = new TaskCompletionSource<bool>();

        _mockErrorHandlingStrategy
            .Setup(x => x.HandleErrorAsync(It.IsAny<FailedMessageDetails>(), It.IsAny<CancellationToken>()))
            .Callback(() => errorHandlingCompleted.TrySetResult(true))
            .ReturnsAsync(ErrorHandlingResult.Discarded);

        _mockConsumer
            .Setup(x => x.HandleMessageAsync(
                It.IsAny<MessageEnvelop<TestMessage>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Test exception"));

        using var cts = CreateDefaultCts();

        var runTask = StartRun(cts);
        await PublishMessageAsync(testMessage);

        await errorHandlingCompleted.WaitAsync(DefaultTimeoutSeconds);

        _mockConsumer.Verify(x => x.HandleMessageAsync(
            It.IsAny<MessageEnvelop<TestMessage>>(),
            It.IsAny<CancellationToken>()),
            Times.Once, "Handler should be called once");

        _mockErrorHandlingStrategy.Verify(x => x.HandleErrorAsync(
            It.Is<FailedMessageDetails>(d => d.Exception is InvalidOperationException),
            It.IsAny<CancellationToken>()),
            Times.Once, "Error handling strategy should be invoked when handler throws");

        await cts.CancelAsync();
        await runTask;
    }

    [Test]
    public async Task RunAsync_WhenMultipleMessagesPublished_ThenShouldProcessAllInSequence()
    {
        var messages = Enumerable.Range(1, 5)
            .Select(i => new TestMessage { Id = Guid.NewGuid(), Content = $"Message {i}" })
            .ToList();

        var receivedMessages = new AsyncThresholdCollection<TestMessage>();

        _mockConsumer
            .Setup(x => x.HandleMessageAsync(
                It.IsAny<MessageEnvelop<TestMessage>>(),
                It.IsAny<CancellationToken>()))
            .Callback<MessageEnvelop<TestMessage>, CancellationToken>((msg, _)
                => receivedMessages.Add(msg.Message))
            .ReturnsAsync(true);

        using var cts = CreateDefaultCts();
        var runTask = StartRun(cts);

        foreach (var message in messages)
        {
            await PublishMessageAsync(message);
        }

        var allReceived = await receivedMessages.WaitUntilCountAtLeastAsync(messages.Count, TimeSpan.FromSeconds(DefaultTimeoutSeconds), cts.Token);
        allReceived.Should().BeTrue("All messages should be received within timeout");

        cts.Cancel();
        await runTask;

        var snapshot = receivedMessages.GetSnapshot();
        snapshot.Count.Should().Be(messages.Count);
        foreach (var originalMessage in messages)
        {
            snapshot.Should().Contain(m =>
                m.Id == originalMessage.Id && m.Content == originalMessage.Content);
        }
    }



    [Test]
    public async Task StopAsync_WhenInFlightMessages_ThenWaitForInFlightMessagesBeforeCompleting()
    {
        var testMessage = new TestMessage { Id = Guid.NewGuid(), Content = "Slow message" };
        var processingStarted = new TaskCompletionSource<bool>();
        var continueProcessing = new TaskCompletionSource<bool>();
        var stopTaskStarted = new TaskCompletionSource<bool>();

        _mockConsumer
            .Setup(x => x.HandleMessageAsync(
                It.IsAny<MessageEnvelop<TestMessage>>(),
                It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                processingStarted.TrySetResult(true);
                continueProcessing.Task.Wait();
            })
            .ReturnsAsync(true);

        using var serviceStopCts = CreateDefaultCts();
        using var processingStopCts = CreateDefaultCts();
        var runTask = _sut.RunAsync(serviceStopCts.Token, processingStopCts.Token);

        await PublishMessageAsync(testMessage);
        await processingStarted.WaitAsync(DefaultTimeoutSeconds);

        processingStopCts.Cancel();

        var stopTask = Task.Run(async () =>
        {
            stopTaskStarted.TrySetResult(true);
            await _sut.StopAsync(serviceStopCts.Token);
        });

        await stopTaskStarted.WaitAsync(5);

        var completedInTime = await Task.WhenAny(stopTask, Task.Delay(50)) == stopTask;
        completedInTime.Should().BeFalse("StopAsync should wait for in-flight messages");

        continueProcessing.SetResult(true);

        await stopTask.WaitAsync(TimeSpan.FromSeconds(5));
        stopTask.IsCompleted.Should().BeTrue("StopAsync should complete after message is processed");

        await serviceStopCts.CancelAsync();
        try
        {
            await runTask;
        }
        catch (OperationCanceledException)
        {
        }
    }

    [Test]
    public async Task RunAsync_WhenHandlerFailsAndErrorStrategyReturnsDiscarded_ThenShouldAckMessage()
    {
        var testMessage = new TestMessage { Id = Guid.NewGuid(), Content = "Discarded message" };
        var errorHandlingCompleted = new TaskCompletionSource<bool>();

        _mockErrorHandlingStrategy
            .Setup(x => x.HandleErrorAsync(It.IsAny<FailedMessageDetails>(), It.IsAny<CancellationToken>()))
            .Callback(() => errorHandlingCompleted.TrySetResult(true))
            .ReturnsAsync(ErrorHandlingResult.Discarded);

        _mockConsumer
            .Setup(x => x.HandleMessageAsync(
                It.IsAny<MessageEnvelop<TestMessage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        using var cts = CreateDefaultCts();
        var runTask = StartRun(cts);

        await PublishMessageAsync(testMessage);
        await errorHandlingCompleted.WaitAsync(DefaultTimeoutSeconds);

        await cts.CancelAsync();
        await runTask;

        var messageCount = await GetQueueMessageCount();
        messageCount.Should().Be(0u, "Message should be acknowledged and removed from queue when discarded");

        _mockConsumer.Verify(x => x.HandleMessageAsync(
            It.IsAny<MessageEnvelop<TestMessage>>(),
            It.IsAny<CancellationToken>()),
            Times.Once, "Handler should be called only once when message is discarded");

        _mockErrorHandlingStrategy.Verify(x => x.HandleErrorAsync(
            It.IsAny<FailedMessageDetails>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task RunAsync_WhenHandlerFailsAndErrorStrategyReturnsSentToDeadLetter_ThenShouldAckMessage()
    {
        var testMessage = new TestMessage { Id = Guid.NewGuid(), Content = "Dead letter message" };
        var errorHandlingCompleted = new TaskCompletionSource<bool>();

        _mockErrorHandlingStrategy
            .Setup(x => x.HandleErrorAsync(It.IsAny<FailedMessageDetails>(), It.IsAny<CancellationToken>()))
            .Callback(() => errorHandlingCompleted.TrySetResult(true))
            .ReturnsAsync(ErrorHandlingResult.SentToDeadLetter);

        _mockConsumer
            .Setup(x => x.HandleMessageAsync(
                It.IsAny<MessageEnvelop<TestMessage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        using var cts = CreateDefaultCts();
        var runTask = StartRun(cts);

        await PublishMessageAsync(testMessage);
        await errorHandlingCompleted.WaitAsync(DefaultTimeoutSeconds);

        await cts.CancelAsync();
        await runTask;

        var messageCount = await GetQueueMessageCount();
        messageCount.Should().Be(0u, "Message should be acknowledged and removed from queue when sent to DLQ");

        _mockConsumer.Verify(x => x.HandleMessageAsync(
            It.IsAny<MessageEnvelop<TestMessage>>(),
            It.IsAny<CancellationToken>()),
            Times.Once, "Handler should be called only once when message is sent to DLQ");

        _mockErrorHandlingStrategy.Verify(x => x.HandleErrorAsync(
            It.IsAny<FailedMessageDetails>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task RunAsync_WhenHandlerFailsAndErrorStrategyReturnsAcknowledged_ThenShouldAckMessage()
    {
        var testMessage = new TestMessage { Id = Guid.NewGuid(), Content = "Acknowledged despite failure" };
        var errorHandlingCompleted = new TaskCompletionSource<bool>();

        _mockErrorHandlingStrategy
            .Setup(x => x.HandleErrorAsync(It.IsAny<FailedMessageDetails>(), It.IsAny<CancellationToken>()))
            .Callback(() => errorHandlingCompleted.TrySetResult(true))
            .ReturnsAsync(ErrorHandlingResult.Acknowledged);

        _mockConsumer
            .Setup(x => x.HandleMessageAsync(
                It.IsAny<MessageEnvelop<TestMessage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        using var cts = CreateDefaultCts();
        var runTask = StartRun(cts);

        await PublishMessageAsync(testMessage);
        await errorHandlingCompleted.WaitAsync(DefaultTimeoutSeconds);

        await cts.CancelAsync();
        await runTask;

        var messageCount = await GetQueueMessageCount();
        messageCount.Should().Be(0u, "Message should be acknowledged and removed from queue");

        _mockConsumer.Verify(x => x.HandleMessageAsync(
            It.IsAny<MessageEnvelop<TestMessage>>(),
            It.IsAny<CancellationToken>()),
            Times.Once, "Handler should be called only once");

        _mockErrorHandlingStrategy.Verify(x => x.HandleErrorAsync(
            It.IsAny<FailedMessageDetails>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task RunAsync_WhenHandlerThrowsException_ThenPassCorrectFailedDetailsToErrorHandlingStrategy()
    {
        var testMessage = new TestMessage { Id = Guid.NewGuid(), Content = "Test message" };
        var expectedException = new InvalidOperationException("Test exception");
        var messageReceived = new TaskCompletionSource<bool>();
        FailedMessageDetails? capturedDetails = null;

        _mockErrorHandlingStrategy
            .Setup(x => x.HandleErrorAsync(It.IsAny<FailedMessageDetails>(), It.IsAny<CancellationToken>()))
            .Callback<FailedMessageDetails, CancellationToken>((details, _) =>
            {
                capturedDetails = details;
                messageReceived.TrySetResult(true);
            })
            .ReturnsAsync(ErrorHandlingResult.Discarded);

        _mockConsumer
            .Setup(x => x.HandleMessageAsync(
                It.IsAny<MessageEnvelop<TestMessage>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        using var cts = CreateDefaultCts();
        var runTask = _sut.RunAsync(cts.Token, cts.Token);

        await PublishMessageAsync(testMessage);
        await messageReceived.WaitAsync(DefaultTimeoutSeconds);

        await cts.CancelAsync();
        await runTask;

        capturedDetails.Should().NotBeNull();
        capturedDetails!.Exception.Should().Be(expectedException);
        capturedDetails.Topic.Should().Be(_exchangeName);
        capturedDetails.ServiceKey.Should().Be(_serviceKey);
        capturedDetails.MessageType.Should().Be<TestMessage>();
        capturedDetails.OriginalMessage.Length.Should().BeGreaterThan(0);
    }

    public record TestMessage
    {
        public Guid Id { get; init; }
        public string Content { get; init; } = string.Empty;
    }
}



