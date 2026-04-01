using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.Abstractions.ErrorHandling;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Routines;
using Minnaloushe.Core.MessageQueues.RabbitMq.Consumers;
using Minnaloushe.Core.Toolbox.TestHelpers;
using Moq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace Minnaloushe.Core.MessageQueue.RabbitMq.Tests.Unit;

[TestFixture]
[Category("Unit")]
public class RabbitMqEngineUnitTests
{
    #region Constants

    private const string ServiceKey = "test-service";

    #endregion

    #region Fields

    private RabbitMqMessageEngine<TestMessage> _sut;
    private Mock<IConnection> _connectionMock;
    private Mock<IChannel> _channelMock;
    private Mock<IConsumer<TestMessage>> _mockConsumer = new();
    private Mock<IErrorHandlingStrategy> _mockErrorHandlingStrategy;
    private AsyncEventingBasicConsumer _capturedConsumer = null!;
    private TaskCompletionSource<bool>? _consumerRegisteredTcs;
    private TaskCompletionSource<bool>? _messageReceivedTcs;
    private TaskCompletionSource<bool>? _basicAckTcs;
    private TaskCompletionSource<bool>? _basicNackTcs;
    private readonly TaskCompletionSource<bool>? _basicCancelTcs = null;

    #endregion

    #region Setups and Teardowns

    [SetUp]
    public async Task SetUp()
    {
        _connectionMock = new Mock<IConnection>();
        _channelMock = new Mock<IChannel>();
        _mockConsumer = new Mock<IConsumer<TestMessage>>();
        _mockErrorHandlingStrategy = new Mock<IErrorHandlingStrategy>();

        _consumerRegisteredTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _channelMock
            .Setup(x => x.BasicConsumeAsync(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object?>>(),
                It.IsAny<IAsyncBasicConsumer>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, bool, string, bool, bool, IDictionary<string, object?>, IAsyncBasicConsumer, CancellationToken>(
                (queue, autoAck, consumerTag, noLocal, exclusive, arguments, consumer, ct) =>
                {
                    _capturedConsumer = (AsyncEventingBasicConsumer)consumer;
                    _consumerRegisteredTcs?.TrySetResult(true);
                })
            .ReturnsAsync("consumer-tag-123");

        _channelMock
            .Setup(x => x.BasicAckAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<ulong, bool, CancellationToken>((tag, multiple, ct) => _basicAckTcs?.TrySetResult(true))
            .Returns(() => ValueTask.CompletedTask);

        _channelMock
            .Setup(x => x.BasicNackAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<ulong, bool, bool, CancellationToken>((tag, multiple, requeue, ct) => _basicNackTcs?.TrySetResult(true))
            .Returns(() => ValueTask.CompletedTask);

        _channelMock
            .Setup(x => x.BasicCancelAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<string, bool, CancellationToken>((tag, requeue, ct) => _basicCancelTcs?.TrySetResult(true))
            .Returns(() => Task.CompletedTask);

        _mockErrorHandlingStrategy
            .Setup(x => x.HandleErrorAsync(It.IsAny<FailedMessageDetails>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ErrorHandlingResult.Discarded);

        _connectionMock.Setup(x => x.CreateChannelAsync(
                It.IsAny<CreateChannelOptions>(),
                It.IsAny<CancellationToken>()
            )
        ).ReturnsAsync(_channelMock.Object);

        _sut = new RabbitMqMessageEngine<TestMessage>(
            "consumer",
            _connectionMock.Object,
            _mockConsumer.Object,
            new MessageQueueOptions
            {
                ServiceKey = ServiceKey
            },
            _mockErrorHandlingStrategy.Object,
            new MessageQueueNamingConventionsProvider(),
            new NullLogger<RabbitMqMessageEngine<TestMessage>>()
        );
    }


    [TearDown]
    public async Task TearDown()
    {

    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
    }

    #endregion

    [Test]
    public async Task RunAsync_WhenMessagePublished_ThenShouldConsumeMessage()
    {
        var testMessage = new TestMessage { Id = Guid.NewGuid(), Content = "Test Content" };
        var messageBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(testMessage));
        _messageReceivedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        _mockConsumer
            .Setup(x => x.HandleMessageAsync(
                It.IsAny<MessageEnvelop<TestMessage>>(),
                It.IsAny<CancellationToken>()))
                .Callback<MessageEnvelop<TestMessage>, CancellationToken>((msg, _) =>
                {
                    msg.Message.Id.Should().Be(testMessage.Id);
                    msg.Message.Content.Should().Be(testMessage.Content);
                    _messageReceivedTcs?.TrySetResult(true);
                })
            .ReturnsAsync(true);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var runTask = Task.Run(async () => await _sut.RunAsync(cts.Token, cts.Token), cts.Token);

        // wait for consumer to be registered before delivering
        await _consumerRegisteredTcs!.WaitAsync(5);

        var deliverEventArgs = new BasicDeliverEventArgs(
            consumerTag: "consumer-tag",
            deliveryTag: 1,
            redelivered: false,
            exchange: "test-exchange",
            routingKey: "test-key",
            properties: null!,
            body: new ReadOnlyMemory<byte>(messageBody),
            cancellationToken: CancellationToken.None);

        await _capturedConsumer.HandleBasicDeliverAsync(
            "consumer-tag",
            deliverEventArgs.DeliveryTag,
            deliverEventArgs.Redelivered,
            deliverEventArgs.Exchange,
            deliverEventArgs.RoutingKey,
            deliverEventArgs.BasicProperties,
            deliverEventArgs.Body,
            CancellationToken.None);

        // Wait for message handling to complete
        await _messageReceivedTcs!.WaitAsync(5);

        await cts.CancelAsync();
        try
        {
            await runTask;
        }
        catch (OperationCanceledException) { }
    }

    [Test]
    public async Task RunAsync_WhenHandlerReturnsTrue_ThenShouldAckMessage()
    {
        var testMessage = new TestMessage { Id = Guid.NewGuid(), Content = "Test Content" };
        var messageBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(testMessage));

        _mockConsumer
            .Setup(x => x.HandleMessageAsync(
                It.IsAny<MessageEnvelop<TestMessage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _basicAckTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var runTask = Task.Run(async () => await _sut.RunAsync(cts.Token, cts.Token), cts.Token);

        // wait for consumer to be registered
        await _consumerRegisteredTcs!.WaitAsync(5);

        var deliverEventArgs = new BasicDeliverEventArgs(
            consumerTag: "consumer-tag",
            deliveryTag: 1,
            redelivered: false,
            exchange: "test-exchange",
            routingKey: "test-key",
            properties: null!,
            body: new ReadOnlyMemory<byte>(messageBody),
            cancellationToken: CancellationToken.None);

        await _capturedConsumer.HandleBasicDeliverAsync(
            "consumer-tag",
            deliverEventArgs.DeliveryTag,
            deliverEventArgs.Redelivered,
            deliverEventArgs.Exchange,
            deliverEventArgs.RoutingKey,
            deliverEventArgs.BasicProperties,
            deliverEventArgs.Body,
            CancellationToken.None);

        // wait for ack to be called
        await _basicAckTcs!.WaitAsync(5);

        await cts.CancelAsync();
        try
        {
            await runTask;
        }
        catch (OperationCanceledException) { }

        _channelMock.Verify(x => x.BasicAckAsync(1, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RunAsync_WhenHandlerReturnsFalse_ThenShouldNackMessage()
    {
        var testMessage = new TestMessage { Id = Guid.NewGuid(), Content = "Test Content" };
        var messageBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(testMessage));

        _mockConsumer
            .Setup(x => x.HandleMessageAsync(
                It.IsAny<MessageEnvelop<TestMessage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Ensure error handling strategy indicates requeue so Nack is expected
        _mockErrorHandlingStrategy
            .Setup(x => x.HandleErrorAsync(It.IsAny<FailedMessageDetails>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ErrorHandlingResult.Requeued);

        _basicNackTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var runTask = Task.Run(async () => await _sut.RunAsync(cts.Token, cts.Token), cts.Token);

        // wait for consumer to be registered
        await _consumerRegisteredTcs!.WaitAsync(5);

        var deliverEventArgs = new BasicDeliverEventArgs(
            consumerTag: "consumer-tag",
            deliveryTag: 1,
            redelivered: false,
            exchange: "test-exchange",
            routingKey: "test-key",
            properties: null!,
            body: new ReadOnlyMemory<byte>(messageBody),
            cancellationToken: CancellationToken.None);

        await _capturedConsumer.HandleBasicDeliverAsync(
            "consumer-tag",
            deliverEventArgs.DeliveryTag,
            deliverEventArgs.Redelivered,
            deliverEventArgs.Exchange,
            deliverEventArgs.RoutingKey,
            deliverEventArgs.BasicProperties,
            deliverEventArgs.Body,
            CancellationToken.None);

        // wait for nack to be called
        await _basicNackTcs!.WaitAsync(5);

        await cts.CancelAsync();
        try
        {
            await runTask;
        }
        catch (OperationCanceledException) { }

        _channelMock.Verify(x => x.BasicNackAsync(1, false, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RunAsync_WhenHandlerThrowsException_ThenShouldNackAndRequeue()
    {
        var testMessage = new TestMessage { Id = Guid.NewGuid(), Content = "Test Content" };
        var messageBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(testMessage));

        _mockConsumer
            .Setup(x => x.HandleMessageAsync(
                It.IsAny<MessageEnvelop<TestMessage>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Handler failed"));

        // Ensure error handling strategy indicates requeue so Nack is expected
        _mockErrorHandlingStrategy
            .Setup(x => x.HandleErrorAsync(It.IsAny<FailedMessageDetails>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ErrorHandlingResult.Requeued);


        _sut = new RabbitMqMessageEngine<TestMessage>(
            "consumer",
            _connectionMock.Object,
            _mockConsumer.Object,
            new MessageQueueOptions
            {
                ServiceKey = ServiceKey,
            },
            _mockErrorHandlingStrategy.Object,
            new MessageQueueNamingConventionsProvider(),
            new NullLogger<RabbitMqMessageEngine<TestMessage>>());

        _basicNackTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var runTask = Task.Run(async () => await _sut.RunAsync(cts.Token, cts.Token), cts.Token);

        // wait for consumer to be registered
        await _consumerRegisteredTcs!.WaitAsync(5);

        var deliverEventArgs = new BasicDeliverEventArgs(
            consumerTag: "consumer-tag",
            deliveryTag: 1,
            redelivered: false,
            exchange: "test-exchange",
            routingKey: "test-key",
            properties: null!,
            body: new ReadOnlyMemory<byte>(messageBody),
            cancellationToken: CancellationToken.None);

        await _capturedConsumer.HandleBasicDeliverAsync(
            "consumer-tag",
            deliverEventArgs.DeliveryTag,
            deliverEventArgs.Redelivered,
            deliverEventArgs.Exchange,
            deliverEventArgs.RoutingKey,
            deliverEventArgs.BasicProperties,
            deliverEventArgs.Body,
            CancellationToken.None);

        // wait for nack to be called
        await _basicNackTcs!.WaitAsync(5);

        await cts.CancelAsync();
        try
        {
            await runTask;
        }
        catch (OperationCanceledException) { }

        _channelMock.Verify(x => x.BasicNackAsync(1, false, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RunAsync_WhenMultipleMessagesPublished_ThenProcessMultipleMessagesInSequence()
    {
        var messageCount = 0;
        var processedIds = new List<Guid>();

        _mockConsumer
            .Setup(x => x.HandleMessageAsync(
                It.IsAny<MessageEnvelop<TestMessage>>(),
                It.IsAny<CancellationToken>()))
            .Callback<MessageEnvelop<TestMessage>, CancellationToken>((msg, _) =>
            {
                Interlocked.Increment(ref messageCount);
                lock (processedIds)
                {
                    processedIds.Add(msg.Message.Id);
                }
            })
            .ReturnsAsync(true);

        _messageReceivedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var runTask = Task.Run(async () => await _sut.RunAsync(cts.Token, cts.Token), cts.Token);

        // wait for consumer to be registered
        await _consumerRegisteredTcs!.WaitAsync(5);

        var messageIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        for (int i = 0; i < messageIds.Length; i++)
        {
            var testMessage = new TestMessage { Id = messageIds[i], Content = $"Message {i}" };
            var messageBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(testMessage));

            var deliverEventArgs = new BasicDeliverEventArgs(
                consumerTag: "consumer-tag",
                deliveryTag: (ulong)(i + 1),
                redelivered: false,
                exchange: "test-exchange",
                routingKey: "test-key",
                properties: null!,
                body: new ReadOnlyMemory<byte>(messageBody),
                cancellationToken: CancellationToken.None);

            await _capturedConsumer.HandleBasicDeliverAsync(
                "consumer-tag",
                deliverEventArgs.DeliveryTag,
                deliverEventArgs.Redelivered,
                deliverEventArgs.Exchange,
                deliverEventArgs.RoutingKey,
                deliverEventArgs.BasicProperties,
                deliverEventArgs.Body,
                CancellationToken.None);
            await Task.Delay(50);
        }
        // wait for all messages to be processed
        await Task.Delay(50 * messageIds.Length); // small pacing to allow messages to be queued
        await cts.CancelAsync();
        try
        {
            await runTask;
        }
        catch (OperationCanceledException) { }

        messageCount.Should().Be(3);
        processedIds.Count.Should().Be(3);
        processedIds.Should().Contain(messageIds[0]);
        processedIds.Should().Contain(messageIds[1]);
        processedIds.Should().Contain(messageIds[2]);
    }

    [Test]
    public async Task RunAsync_WhenStopping_ThenShouldWaitForInFlightMessagesBeforeCompleting()
    {
        var messageStarted = new TaskCompletionSource<bool>();
        var messageCanComplete = new TaskCompletionSource<bool>();

        _mockConsumer
            .Setup(x => x.HandleMessageAsync(
                It.IsAny<MessageEnvelop<TestMessage>>(),
                It.IsAny<CancellationToken>()))
            .Returns<MessageEnvelop<TestMessage>, CancellationToken>(async (_, _) =>
            {
                messageStarted.SetResult(true);
                await messageCanComplete.Task;
                return true;
            });

        using var serviceCts = new CancellationTokenSource();
        using var processingCts = new CancellationTokenSource();
        var runTask = Task.Run(async () => await _sut.RunAsync(serviceCts.Token, processingCts.Token));

        await Task.Delay(100);

        var testMessage = new TestMessage { Id = Guid.NewGuid(), Content = "Test Content" };
        var messageBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(testMessage));

        var deliverEventArgs = new BasicDeliverEventArgs(
            consumerTag: "consumer-tag",
            deliveryTag: 1,
            redelivered: false,
            exchange: "test-exchange",
            routingKey: "test-key",
            properties: null!,
            body: new ReadOnlyMemory<byte>(messageBody),
            cancellationToken: CancellationToken.None);

        await _capturedConsumer.HandleBasicDeliverAsync(
            "consumer-tag",
            deliverEventArgs.DeliveryTag,
            deliverEventArgs.Redelivered,
            deliverEventArgs.Exchange,
            deliverEventArgs.RoutingKey,
            deliverEventArgs.BasicProperties,
            deliverEventArgs.Body,
            CancellationToken.None);

        await messageStarted.Task;
        await processingCts.CancelAsync();

        await _consumerRegisteredTcs!.WaitAsync(5);
        runTask.IsCompleted.Should().BeFalse();

        messageCanComplete.SetResult(true);

        await Task.WhenAny(runTask, Task.Delay(2000));
        runTask.IsCompleted.Should().BeTrue();

        _channelMock.Verify(
            x => x.BasicCancelAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    public record TestMessage
    {
        public Guid Id { get; init; }
        public string Content { get; init; } = string.Empty;
    }
}


