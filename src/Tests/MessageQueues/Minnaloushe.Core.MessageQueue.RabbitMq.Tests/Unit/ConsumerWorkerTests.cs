using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.ClientProviders.Abstractions;
using Minnaloushe.Core.ClientProviders.Abstractions.ClientLease;
using Minnaloushe.Core.MessageQueue.RabbitMq.Tests.TestClasses;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.Abstractions.ErrorHandling;
using Minnaloushe.Core.Toolbox.TestHelpers;
using Moq;
using System.Diagnostics;

namespace Minnaloushe.Core.MessageQueue.RabbitMq.Tests.Unit;

[TestFixture]
[Category("Unit")]
public class ConsumerWorkerTests
{
    #region Fixture members

    #region Constants

    private const string ConsumerName = "test-consumer";

    #endregion

    #region Fields

    private ConsumerWorker<TestMessage, object> _sut;
    private Mock<IClientProvider<object>> _mockClientProvider;
    private Mock<IConsumer<TestMessage>> _mockConsumer;
    private Mock<IOptionsMonitor<MessageQueueOptions>> _mockOptionsMonitor;
    private Mock<IMessageEngineFactory<TestMessage, object>> _mockEngineFactory;
    private Mock<IMessageEngine> _mockEngine;
    private Mock<IErrorHandlingStrategy> _mockErrorHandlingStrategy;
    private readonly Mock<IServiceScopeFactory> _mockServiceScopeFactory = new();
    private readonly Mock<IServiceScope> _mockServiceScope = new();
    private readonly Mock<IServiceProvider> _mockServiceProvider = new();
    private NullLogger<ConsumerWorkerTests> _logger;
    private MessageQueueOptions _messageQueueOptions;
    private int _engineCreateCount;
    private int _engineCreateTarget;
    private TaskCompletionSource<bool>? _acquireTcs;
    private TaskCompletionSource<bool>? _engineCreatedTcs;
    private TaskCompletionSource<bool>? _engineCreateReachedTcs;
    private TaskCompletionSource<bool>? _engineRunStartedTcs;

    #endregion

    [SetUp]
    public void SetUp()
    {
        _mockClientProvider = new Mock<IClientProvider<object>>();
        _mockConsumer = new Mock<IConsumer<TestMessage>>();
        _mockOptionsMonitor = new Mock<IOptionsMonitor<MessageQueueOptions>>();
        _mockEngineFactory = new Mock<IMessageEngineFactory<TestMessage, object>>();
        _mockEngine = new Mock<IMessageEngine>();
        _mockErrorHandlingStrategy = new Mock<IErrorHandlingStrategy>();
        _logger = new NullLogger<ConsumerWorkerTests>();

        _mockServiceScopeFactory.Setup(x => x.CreateScope()).Returns(_mockServiceScope.Object);
        _mockServiceScope.Setup(x => x.ServiceProvider).Returns(_mockServiceProvider.Object);
        _mockServiceProvider.Setup(x => x.GetService(typeof(IConsumer<TestMessage>))).Returns(_mockConsumer.Object);

        _messageQueueOptions = new MessageQueueOptions
        {
            ServiceKey = ConsumerName,
            ConsumerErrorDelay = TimeSpan.FromMilliseconds(100)
        };

        _mockOptionsMonitor
            .Setup(x => x.Get(ConsumerName))
            .Returns(_messageQueueOptions);

        var acquireCount = 0;
        _acquireTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _engineCreatedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _engineCreateCount = 0;
        _engineCreateTarget = 0;
        _engineCreateReachedTcs = null;
        _mockClientProvider
            .Setup(x => x.Acquire())
            .Callback(() => _acquireTcs?.TrySetResult(true))
            .Returns(() =>
            {
                var client = new object();
                var holder = new ClientHolder<object>(client, ++acquireCount);
                holder.Retain();
                return new ManagedClientLease<object>(holder);
            });

        _mockEngineFactory
            .Setup(x => x.CreateEngine(
                ConsumerName,
                It.IsAny<object>(),
                _mockConsumer.Object,
                _messageQueueOptions,
                _mockErrorHandlingStrategy.Object))
            .Callback(() =>
            {
                _engineCreateCount++;
                _engineCreatedTcs?.TrySetResult(true);
                if (_engineCreateTarget > 0 && _engineCreateCount >= _engineCreateTarget)
                {
                    _engineCreateReachedTcs?.TrySetResult(true);
                }
            })
            .Returns(_mockEngine.Object);

        _engineRunStartedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _mockEngine
            .Setup(x => x.RunAsync(It.IsAny<CancellationToken>(), It.IsAny<CancellationToken>()))
            .Callback(() => _engineRunStartedTcs?.TrySetResult(true))
            .Returns(Task.CompletedTask);

        _sut = new ConsumerWorker<TestMessage, object>(
            ConsumerName,
            _mockClientProvider.Object,
            _mockOptionsMonitor.Object,
            _mockEngineFactory.Object,
            _mockErrorHandlingStrategy.Object,
            _mockServiceScopeFactory.Object,
            _logger
        );
    }

    [TearDown]
    public async Task TearDown()
    {
        await _sut.StopAsync();
        await (_sut as IAsyncDisposable).DisposeAsync();
    }

    #endregion

    [Test]
    public async Task StartAsync_WhenCalled_ThenShouldStartConsumerSuccessfully()
    {
        // Arrange

        // Act
        await _sut.StartAsync();
        // wait for at least one acquire and engine creation
        await _acquireTcs!.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await _engineCreatedTcs!.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        _mockClientProvider.Verify(x => x.Acquire(), Times.AtLeastOnce);
        _mockEngineFactory.Verify(
            x => x.CreateEngine(ConsumerName, It.IsAny<object>(), _mockConsumer.Object, _messageQueueOptions, _mockErrorHandlingStrategy.Object),
            Times.AtLeastOnce);
    }

    [Test]
    public async Task StartAsync_WhenCalledTwice_ThenShouldThrowException()
    {
        // Arrange
        await _sut.StartAsync();

        // Act
        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await _sut.StartAsync());

        // Assert
        exception.Message.Should().Be("Consumer has already been started.");
    }

    [Test]
    public async Task StopAsync_WhenStarted_ThenShouldStopConsumer()
    {
        // Arrange
        var engineRunTcs = new TaskCompletionSource<bool>();

        _mockEngine
            .Setup(x => x.RunAsync(It.IsAny<CancellationToken>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await engineRunTcs.Task;
            });

        await _sut.StartAsync();
        // wait for client acquire and engine creation so the engine RunAsync callback has executed
        await _acquireTcs.WaitAsync(2);
        await _engineCreatedTcs.WaitAsync(2);

        // Act
        var stopTask = _sut.StopAsync();
        engineRunTcs.SetResult(true);
        await stopTask;

        // Assert
        stopTask.IsCompleted.Should().BeTrue();
    }

    [Test]
    public async Task StopAsync_WhenNotStarted_ThenShouldDoNothing()
    {
        // Arrange

        // Act
        await _sut.StopAsync();

        // Assert
        _mockClientProvider.Verify(x => x.Acquire(), Times.Never);
    }

    [Test]
    public async Task MainOuterLoop_WhenRunning_ThenShouldAcquireClientAndCreateEngine()
    {
        // Arrange

        // Act
        await _sut.StartAsync();
        await _acquireTcs!.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await _engineCreatedTcs!.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await _sut.StopAsync();

        // Assert
        _mockClientProvider.Verify(x => x.Acquire(), Times.AtLeastOnce);
        _mockEngineFactory.Verify(
            x => x.CreateEngine(ConsumerName, It.IsAny<object>(), _mockConsumer.Object, _messageQueueOptions, _mockErrorHandlingStrategy.Object),
            Times.AtLeastOnce);
        _mockEngine.Verify(
            x => x.RunAsync(It.IsAny<CancellationToken>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Test]
    public async Task MainOuterLoop_WhenEngineRuns_ThenShouldReleaseClientAfterEngineRun()
    {
        // Arrange
        var engineRunCompleted = false;
        var acquireCount = 0;

        _mockClientProvider
            .Setup(x => x.Acquire())
            .Callback(() =>
            {
                acquireCount++;
                _acquireTcs!.TrySetResult(true);
            })
            .Returns(() =>
            {
                var client = new object();
                var holder = new ClientHolder<object>(client, acquireCount);
                holder.Retain();
                return new ManagedClientLease<object>(holder);
            });

        _mockEngine
            .Setup(x => x.RunAsync(It.IsAny<CancellationToken>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Delay(100);
                engineRunCompleted = true;
            });

        // Act
        await _sut.StartAsync();
        await _acquireTcs!.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await _sut.StopAsync();

        // Assert
        engineRunCompleted.Should().BeTrue();
        acquireCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task MainOuterLoop_WhenEngineThrowsException_ThenShouldRetry()
    {
        var callCount = 0;
        var maxCalls = 3;
        // Wait until engine factory has been invoked maxCalls times
        _engineCreateCount = 0;
        _engineCreateTarget = maxCalls;
        _engineCreateReachedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        _mockEngine
            .Setup(x => x.RunAsync(It.IsAny<CancellationToken>(), It.IsAny<CancellationToken>()))
            .Returns<CancellationToken, CancellationToken>(async (serviceToken, _) =>
            {
                callCount++;
                if (callCount < maxCalls)
                {
                    throw new InvalidOperationException("Engine error");
                }

                try
                {
                    // ReSharper disable once PossiblyMistakenUseOfCancellationToken
                    await Task.Delay(Timeout.Infinite, serviceToken);
                }
                catch (OperationCanceledException)
                {
                }
            });

        await _sut.StartAsync();

        await _engineCreateReachedTcs.WaitAsync(5);

        await _sut.StopAsync();

        _mockEngineFactory.Verify(
            x => x.CreateEngine(ConsumerName, It.IsAny<object>(), _mockConsumer.Object, _messageQueueOptions, _mockErrorHandlingStrategy.Object),
            Times.AtLeast(maxCalls));
    }

    [Test]
    public async Task MainOuterLoop_WhenErrorOccurs_ThenShouldWaitBeforeRetry()
    {
        var callCount = 0;
        var timestamps = new List<DateTime>();

        _mockEngine
            .Setup(x => x.RunAsync(It.IsAny<CancellationToken>(), It.IsAny<CancellationToken>()))
            .Returns<CancellationToken, CancellationToken>(async (serviceToken, _) =>
            {
                timestamps.Add(DateTime.UtcNow);
                callCount++;
                if (callCount < 2)
                {
                    throw new InvalidOperationException("Engine error");
                }

                try
                {
                    // ReSharper disable once PossiblyMistakenUseOfCancellationToken
                    await Task.Delay(Timeout.Infinite, serviceToken);
                }
                catch (OperationCanceledException)
                {
                }
            });

        await _sut.StartAsync();

        _engineCreateCount = 0;
        _engineCreateTarget = 2;
        _engineCreateReachedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await _engineCreateReachedTcs!.Task.WaitAsync(TimeSpan.FromSeconds(5));

        if (timestamps.Count >= 2)
        {
            var delay = timestamps[1] - timestamps[0];
            delay.TotalMilliseconds.Should().BeGreaterThanOrEqualTo(_messageQueueOptions.ConsumerErrorDelay.TotalMilliseconds * 0.9);
        }

        await _sut.StopAsync();
    }

    [Test]
    public async Task MainOuterLoop_WhenCancellationRequested_ThenShouldStopRetrying()
    {
        _mockEngine
            .Setup(x => x.RunAsync(It.IsAny<CancellationToken>(), It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken _, CancellationToken _) =>
            {
                await Task.Delay(50);
                throw new InvalidOperationException("Engine error");
            });

        // Wait for exactly one engine creation, then stop and ensure no retries start afterwards
        _engineCreateCount = 0;
        _engineCreateTarget = 1;
        _engineCreateReachedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        await _sut.StartAsync();

        // wait until the engine has been created once
        await _engineCreateReachedTcs!.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await _sut.StopAsync();

        // short grace so any in-flight retry would start if Stop did not cancel properly
        await Task.Delay(200);
        _engineCreateCount.Should().Be(1);
    }

    [Test]
    public async Task MainOuterLoop_WhenOperationCanceled_ThenShouldHandleGracefully()
    {
        _mockEngine
            .Setup(x => x.RunAsync(It.IsAny<CancellationToken>(), It.IsAny<CancellationToken>()))
            .Returns<CancellationToken, CancellationToken>((ct1, _) => Task.FromCanceled(ct1));

        await _sut.StartAsync();

        await _acquireTcs!.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await _sut.StopAsync();

        _mockClientProvider.Verify(x => x.Acquire(), Times.AtLeastOnce);
    }

    [Test]
    public async Task StartAsync_WhenProvidedCancellationToken_ThenShouldUseIt()
    {
        using var cts = new CancellationTokenSource();

        await _sut.StartAsync(cts.Token);

        await Task.Delay(100);

        await cts.CancelAsync();

        await Task.Delay(100);

        _mockClientProvider.Verify(x => x.Acquire(), Times.AtLeastOnce);
    }

    [Test]
    public async Task MainOuterLoop_WhenCreatingEngine_ThenShouldCreateLinkedCancellationTokenWithLeaseToken()
    {
        CancellationToken capturedServiceToken = CancellationToken.None;
        CancellationToken capturedProcessingToken = CancellationToken.None;

        _mockEngine
            .Setup(x => x.RunAsync(It.IsAny<CancellationToken>(), It.IsAny<CancellationToken>()))
            .Callback<CancellationToken, CancellationToken>((serviceToken, processingToken) =>
            {
                capturedServiceToken = serviceToken;
                capturedProcessingToken = processingToken;
                _engineRunStartedTcs?.TrySetResult(true);
            })
            .Returns(Task.CompletedTask);

        await _sut.StartAsync();

        await _acquireTcs!.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await _engineRunStartedTcs!.Task.WaitAsync(TimeSpan.FromSeconds(2));

        capturedServiceToken.Should().NotBe(CancellationToken.None);
        capturedProcessingToken.Should().NotBe(CancellationToken.None);

        await _sut.StopAsync();
    }

    [Test]
    public async Task MainOuterLoop_WhenEngineThrows_ThenShouldReleaseClient()
    {
        var acquireCount = 0;

        _mockClientProvider
            .Setup(x => x.Acquire())
            .Callback(() =>
            {
                acquireCount++;
                _acquireTcs!.TrySetResult(true);
            })
            .Returns(() =>
            {
                var client = new object();
                var holder = new ClientHolder<object>(client, acquireCount);
                holder.Retain();
                return new ManagedClientLease<object>(holder);
            });

        _mockEngine
            .Setup(x => x.RunAsync(It.IsAny<CancellationToken>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Engine error"));

        await _sut.StartAsync();

        await _acquireTcs!.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await _sut.StopAsync();

        acquireCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task MainOuterLoop_WhenNotStopped_ThenShouldContinueRunningUntilStopRequested()
    {
        var runCount = 0;
        var tcs = new TaskCompletionSource<bool>();

        _mockEngine
            .Setup(x => x.RunAsync(It.IsAny<CancellationToken>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                runCount++;
                if (runCount >= 3)
                {
                    await tcs.Task;
                }
                await Task.Delay(5);
            });

        await _sut.StartAsync();

        // wait for runCount to reach 3
        var sw3 = Stopwatch.StartNew();
        while (runCount < 3 && sw3.Elapsed < TimeSpan.FromSeconds(2))
        {
            await Task.Delay(10);
        }

        runCount.Should().BeGreaterThanOrEqualTo(3);

        tcs.SetResult(true);
        await _sut.StopAsync();
    }

    [Test]
    public async Task StopAsync_WhenRunning_ThenShouldWaitForEngineCompletionBeforeReturning()
    {
        var engineRunning = true;
        var tcs = new TaskCompletionSource<bool>();

        _mockEngine
            .Setup(x => x.RunAsync(It.IsAny<CancellationToken>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await tcs.Task;
                engineRunning = false;
            });

        await _sut.StartAsync();

        await _acquireTcs!.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var stopTask = _sut.StopAsync();

        await Task.Delay(100);
        engineRunning.Should().BeTrue();

        tcs.SetResult(true);
        await stopTask;

        engineRunning.Should().BeFalse();
    }
}