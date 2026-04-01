using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Minnaloushe.Core.ClientProviders.Abstractions;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Extensions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.RabbitMq.Extensions;
using Minnaloushe.Core.MessageQueues.RabbitMq.Producers;
using Minnaloushe.Core.MessageQueues.Routines;
using Minnaloushe.Core.Tests.Helpers;
using Minnaloushe.Core.Toolbox.AsyncInitializer.Extensions;
using Minnaloushe.Core.Toolbox.TestHelpers;
using RabbitMQ.Client;
using System.Text;

namespace Minnaloushe.Core.MessageQueue.RabbitMq.Tests.Integration;

/// <summary>
/// Integration tests for RabbitMQ Dead Letter Queue (DLQ) error handling.
/// Tests that failed messages are properly sent to the dead letter queue.
/// </summary>
[TestFixture]
[Category("TestContainers")]
[Category("Integration")]
public class RabbitMqDeadLetterIntegrationTests
{
    private TestHost _testHost = null!;
    private readonly string _connectionName = Helpers.UniqueString("rabbit-dlq-connection");
    private readonly string _consumerName = Helpers.UniqueString("dlq-test-consumer");
    private readonly string _serviceKey = Helpers.UniqueString("dlq-test-queue");

    private const int MessageDelayMs = 3000;
    private const int WaitTimeoutSeconds = 10;
    private const int DlqPublishDelayMs = 1000;
    private const int DlqRetryDelayMs = 500;

    public static readonly AsyncThresholdCollection<DlqTestMessage> ReceivedMessages = new();
    public static readonly AsyncThresholdCollection<DlqTestMessage> FailedMessages = new();

    public static bool ShouldFail { get; set; }
    public static bool ShouldThrow { get; set; }

    private object AppSettings =>
        MqHelpers.CreateAppSettings(
            [
                MqHelpers.CreateConnection(_connectionName,
                    "rabbit",
                    serviceKey: _serviceKey,
                    container: GlobalFixture.RabbitMqInstance1
                    )
            ],
            [
                MqHelpers.CreateConsumer(_consumerName,
                    _connectionName,
                    errorHandling: "DeadLetter")
            ]);

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        ReceivedMessages.Clear();
        FailedMessages.Clear();
        ShouldFail = false;
        ShouldThrow = false;

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
                    builder.SetMinimumLevel(LogLevel.Debug);
                });

                services.ConfigureAsyncInitializers();

                services.AddMessageQueues(configuration)
                    .AddRabbitMqClientProviders()
                    .AddRabbitMqConsumers()
                    .AddConsumer<DlqTestMessage, DlqTestConsumerHandler>(_consumerName)
                    .AddRabbitMqProducers()
                    .AddProducer<DlqTestMessage>(_connectionName)
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
        FailedMessages.Clear();
        ShouldFail = false;
        ShouldThrow = false;
    }

    private string GetDlqName()
    {
        var queueName = $"{MqNaming.GetSafeName<DlqTestMessage>()}.{_serviceKey}";
        return $"{queueName}.dlt";
    }

    private async Task<BasicGetResult?> GetDlqMessageAsync(string dlqName, string messageContent)
    {
        var clientProvider = _testHost.Services.GetRequiredKeyedService<IClientProvider<IConnection>>(_connectionName);
        using var lease = clientProvider.Acquire();
        await using var channel = await lease.Client.CreateChannelAsync();

        BasicGetResult? dlqMessage = null;
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(WaitTimeoutSeconds));

        while (!cts.IsCancellationRequested && dlqMessage == null)
        {
            try
            {
                dlqMessage = await channel.BasicGetAsync(dlqName, autoAck: true);
                if (dlqMessage != null)
                {
                    var content = Encoding.UTF8.GetString(dlqMessage.Body.ToArray());
                    if (content.Contains(messageContent))
                    {
                        break;
                    }
                    dlqMessage = null;
                }
            }
            catch (RabbitMQ.Client.Exceptions.OperationInterruptedException)
            {
                await Task.Delay(DlqRetryDelayMs, cts.Token);
            }
        }

        return dlqMessage;
    }

    [Test]
    public async Task PublishAsync_WhenHandlerReturnsFalse_ThenShouldSendToDeadLetterQueue()
    {
        ShouldFail = true;

        var producer = _testHost.Services.GetRequiredService<IProducer<DlqTestMessage>>();
        var messageContent = $"FailedMessage-{Guid.NewGuid()}";
        var message = new DlqTestMessage { Content = messageContent };

        await producer.PublishAsync(message, cancellationToken: CancellationToken.None);

        await FailedMessages.WaitUntilCountAtLeastAsync(1, TimeSpan.FromSeconds(WaitTimeoutSeconds));

        await Task.Delay(DlqPublishDelayMs);

        var dlqName = GetDlqName();
        var dlqMessage = await GetDlqMessageAsync(dlqName, messageContent);

        dlqMessage.Should().NotBeNull("Message should be found in DLQ");
        var dlqContent = Encoding.UTF8.GetString(dlqMessage!.Body.ToArray());
        dlqContent.Should().Contain(messageContent, "DLQ message should contain original message content");
    }

    [Test]
    public async Task PublishAsync_WhenHandlerThrowsException_ThenShouldSendToDeadLetterQueue()
    {
        ShouldThrow = true;

        var producer = _testHost.Services.GetRequiredService<IProducer<DlqTestMessage>>();
        var messageContent = $"ExceptionMessage-{Guid.NewGuid()}";
        var message = new DlqTestMessage { Content = messageContent };

        await producer.PublishAsync(message, cancellationToken: CancellationToken.None);

        await FailedMessages.WaitUntilCountAtLeastAsync(1, TimeSpan.FromSeconds(WaitTimeoutSeconds));

        await Task.Delay(DlqPublishDelayMs);

        var dlqName = GetDlqName();
        var dlqMessage = await GetDlqMessageAsync(dlqName, messageContent);

        dlqMessage.Should().NotBeNull("Message should be found in DLQ after exception");
        var dlqContent = Encoding.UTF8.GetString(dlqMessage!.Body.ToArray());
        dlqContent.Should().Contain(messageContent, "DLQ message should contain original message content");
    }

    [Test]
    public async Task PublishAsync_WhenHandlerSucceeds_ThenShouldNotSendToDeadLetterQueue()
    {
        var producer = _testHost.Services.GetRequiredService<IProducer<DlqTestMessage>>();
        var messageContent = $"SuccessMessage-{Guid.NewGuid()}";
        var message = new DlqTestMessage { Content = messageContent };

        await producer.PublishAsync(message, cancellationToken: CancellationToken.None);

        var received = await ReceivedMessages.WaitUntilCountAtLeastAsync(1, TimeSpan.FromSeconds(WaitTimeoutSeconds));
        received.Should().BeTrue("Message should be successfully processed");

        var dlqName = GetDlqName();

        var clientProvider = _testHost.Services.GetRequiredKeyedService<IClientProvider<IConnection>>(_connectionName);
        using var lease = clientProvider.Acquire();
        await using var channel = await lease.Client.CreateChannelAsync();

        BasicGetResult? dlqMessage = null;
        try
        {
            dlqMessage = await channel.BasicGetAsync(dlqName, autoAck: false);
        }
        catch
        {
        }

        if (dlqMessage != null)
        {
            var dlqContent = Encoding.UTF8.GetString(dlqMessage.Body.ToArray());
            dlqContent.Should().NotContain(messageContent, "Successfully processed message should NOT be in DLQ");
            await channel.BasicNackAsync(dlqMessage.DeliveryTag, multiple: false, requeue: true);
        }
    }

    [Test]
    public async Task DlqMessage_WhenPublished_ThenShouldContainOriginalHeaders()
    {
        ShouldFail = true;

        var producer = _testHost.Services.GetRequiredService<IProducer<DlqTestMessage>>();
        var messageContent = $"HeaderTestMessage-{Guid.NewGuid()}";
        var correlationId = Guid.NewGuid().ToString();
        var message = new DlqTestMessage { Content = messageContent };

        var originalHeaders = new Dictionary<string, string>
        {
            ["x-correlation-id"] = correlationId,
            ["x-custom-header"] = "test-value"
        };

        await producer.PublishAsync(message, null, originalHeaders, CancellationToken.None);

        await FailedMessages.WaitUntilCountAtLeastAsync(1, TimeSpan.FromSeconds(WaitTimeoutSeconds));

        await Task.Delay(DlqPublishDelayMs);

        var dlqName = GetDlqName();
        var dlqMessage = await GetDlqMessageAsync(dlqName, messageContent);

        dlqMessage.Should().NotBeNull("Message should be found in DLQ");
        dlqMessage!.BasicProperties.Headers.Should().NotBeNull("DLQ message should have headers");

        var headerKeys = dlqMessage.BasicProperties.Headers!.Keys.Cast<string>().ToList();

        var hasCorrelationHeader = headerKeys.Any(k =>
            k.Contains("correlation-id", StringComparison.OrdinalIgnoreCase));

        hasCorrelationHeader.Should().BeTrue("DLQ message should contain correlation header");
    }

    [Test]
    public async Task DlqMessage_WhenPublished_ThenShouldContainErrorMetadata()
    {
        ShouldThrow = true;

        var producer = _testHost.Services.GetRequiredService<IProducer<DlqTestMessage>>();
        var messageContent = $"ErrorMetadataMessage-{Guid.NewGuid()}";
        var message = new DlqTestMessage { Content = messageContent };

        await producer.PublishAsync(message, cancellationToken: CancellationToken.None);

        await FailedMessages.WaitUntilCountAtLeastAsync(1, TimeSpan.FromSeconds(WaitTimeoutSeconds));

        await Task.Delay(DlqPublishDelayMs);

        var dlqName = GetDlqName();
        var dlqMessage = await GetDlqMessageAsync(dlqName, messageContent);

        dlqMessage.Should().NotBeNull("Message should be found in DLQ");
        dlqMessage!.BasicProperties.Headers.Should().NotBeNull("DLQ message should have headers");

        var headerKeys = dlqMessage.BasicProperties.Headers!.Keys.Cast<string>().ToList();

        headerKeys.Should().Contain("dlt-original-topic", "Should contain original topic header");
        headerKeys.Should().Contain("dlt-failure-timestamp", "Should contain failure timestamp header");
        headerKeys.Should().Contain("dlt-exception-type", "Should contain exception type header");
        headerKeys.Should().Contain("dlt-exception-message", "Should contain exception message header");
    }

    [Test]
    public async Task PublishAsync_WhenMultipleMessagesFail_ThenAllShouldBeInDeadLetterQueue()
    {
        ShouldFail = true;

        var producer = _testHost.Services.GetRequiredService<IProducer<DlqTestMessage>>();
        var messages = Enumerable.Range(1, 3)
            .Select(i => new DlqTestMessage { Content = $"BatchFail-{i}-{Guid.NewGuid()}" })
            .ToList();

        foreach (var message in messages)
        {
            await producer.PublishAsync(message, cancellationToken: CancellationToken.None);
        }

        await FailedMessages.WaitUntilCountAtLeastAsync(3, TimeSpan.FromSeconds(15));

        await Task.Delay(2000);

        var dlqName = GetDlqName();

        var clientProvider = _testHost.Services.GetRequiredKeyedService<IClientProvider<IConnection>>(_connectionName);
        using var lease = clientProvider.Acquire();
        using var channel = await lease.Client.CreateChannelAsync();

        var dlqMessages = new List<string>();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        while (!cts.IsCancellationRequested)
        {
            try
            {
                var dlqMessage = await channel.BasicGetAsync(dlqName, autoAck: true);
                if (dlqMessage != null)
                {
                    dlqMessages.Add(Encoding.UTF8.GetString(dlqMessage.Body.ToArray()));
                }
                else
                {
                    if (dlqMessages.Count >= 3)
                    {
                        break;
                    }
                    await Task.Delay(DlqRetryDelayMs, cts.Token);
                }
            }
            catch (RabbitMQ.Client.Exceptions.OperationInterruptedException)
            {
                await Task.Delay(DlqRetryDelayMs, cts.Token);
            }
        }

        dlqMessages.Count.Should().BeGreaterThanOrEqualTo(3, "All failed messages should be in DLQ");

        foreach (var originalMessage in messages)
        {
            dlqMessages.Should().Contain(m => m.Contains(originalMessage.Content),
                $"DLQ should contain message: {originalMessage.Content}");
        }
    }
}

/// <summary>
/// Test message class for DLQ tests.
/// </summary>
public class DlqTestMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Content { get; init; } = string.Empty;
}

/// <summary>
/// Test consumer handler that can be configured to fail for DLQ testing.
/// </summary>
public class DlqTestConsumerHandler(ILogger<DlqTestConsumerHandler> logger) : IConsumer<DlqTestMessage>
{
    public Task<bool> HandleMessageAsync(MessageEnvelop<DlqTestMessage> envelop, CancellationToken cancellationToken = default)
    {
#pragma warning disable CA1873
        logger.LogInformation("DLQ test consumer received message: {Content}, ShouldFail: {ShouldFail}, ShouldThrow: {ShouldThrow}",
            envelop.Message.Content, RabbitMqDeadLetterIntegrationTests.ShouldFail, RabbitMqDeadLetterIntegrationTests.ShouldThrow);

        if (RabbitMqDeadLetterIntegrationTests.ShouldThrow)
        {
            RabbitMqDeadLetterIntegrationTests.FailedMessages.Add(envelop.Message);
            throw new InvalidOperationException($"Intentional test exception for message: {envelop.Message.Content}");
        }

        if (RabbitMqDeadLetterIntegrationTests.ShouldFail)
        {
            RabbitMqDeadLetterIntegrationTests.FailedMessages.Add(envelop.Message);
            logger.LogWarning("Handler returning false for message: {Content}", envelop.Message.Content);
            return Task.FromResult(false);
        }

        RabbitMqDeadLetterIntegrationTests.ReceivedMessages.Add(envelop.Message);
        logger.LogInformation("Handler successfully processed message: {Content}", envelop.Message.Content);
        return Task.FromResult(true);
#pragma warning restore CA1873
    }
}

