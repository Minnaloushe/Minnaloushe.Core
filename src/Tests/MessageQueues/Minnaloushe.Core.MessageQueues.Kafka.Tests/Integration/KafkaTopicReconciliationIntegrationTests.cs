using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Extensions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Kafka.Extensions;
using Minnaloushe.Core.MessageQueues.Kafka.Consumers.Extensions;
using Minnaloushe.Core.MessageQueues.Routines;
using Minnaloushe.Core.Tests.Helpers;
using Minnaloushe.Core.Toolbox.AsyncInitializer.Extensions;
using Minnaloushe.Core.Toolbox.JsonConfiguration;
using Minnaloushe.Core.Toolbox.TestHelpers;
using System.Globalization;

namespace Minnaloushe.Core.MessageQueues.Kafka.Tests.Integration;

/// <summary>
/// Tests that verify the consumer initializer expands partitions and updates topic configuration
/// when a topic was previously created with different parameters (e.g., by a producer using connection defaults).
/// </summary>
[TestFixture]
[Category("Integration")]
[Category("TestContainers")]
public class KafkaTopicReconciliationExpandTests
{
    #region Constants

    private const int InitialNumPartitions = 1;
    private const short InitialReplicationFactor = 1;
    private const int ConsumerDesiredPartitions = 3;
    private const string ConsumerRetentionTime = "2.00:00:00";
    private const long ConsumerRetentionBytes = 1_073_741_824;
    private const string ConsumerCleanupPolicy = "Delete";
    private const string ConsumerDeleteRetentionTime = "0.12:00:00";

    #endregion

    #region Fields

    private TestHost _testHost = null!;
    private string _connectionName = Helpers.UniqueString("kafka-reconcile-expand");
    private string _consumerName = Helpers.UniqueString("consumer-reconcile-expand");
    private string _serviceKey = Helpers.UniqueString("topic-reconcile-expand");
    private string _topicName = null!;

    #endregion

    #region Helper Classes

    public class ReconciliationExpandMessage
    {
        public string Data { get; init; } = string.Empty;
    }

    public class ReconciliationExpandConsumer(ILogger<ReconciliationExpandConsumer> logger)
        : IConsumer<ReconciliationExpandMessage>
    {
        public Task<bool> HandleMessageAsync(
            MessageEnvelop<ReconciliationExpandMessage> envelop,
            CancellationToken cancellationToken = default)
        {
            logger.ReceivedMessage(envelop.Message.Data);
            return Task.FromResult(true);
        }
    }

    #endregion

    #region Properties

    private object AppSettings => new
    {
        MessageQueues = new
        {
            Connections = new[]
            {
                new
                {
                    Name = _connectionName,
                    Type = "kafka",
                    ConnectionString = GlobalFixture.Kafka1.Instance.GetBootstrapAddress(),
                    ServiceKey = _serviceKey,
                    ErrorHandling = "NackAndDiscard"
                }
            },
            Consumers = new[]
            {
                new
                {
                    Name = _consumerName,
                    ConnectionName = _connectionName,
                    Parallelism = 1,
                    ErrorHandling = "NackAndDiscard",
                    Parameters = new
                    {
                        TopicConfiguration = new
                        {
                            NumPartitions = ConsumerDesiredPartitions,
                            ReplicationFactor = InitialReplicationFactor,
                            RetentionTime = ConsumerRetentionTime,
                            RetentionBytes = ConsumerRetentionBytes,
                            CleanUpPolicy = ConsumerCleanupPolicy,
                            DeleteRetentionTime = ConsumerDeleteRetentionTime
                        }
                    }
                }
            }
        },
        AsyncInitializer = new
        {
            Enabled = true,
            Timeout = TimeSpan.FromMinutes(2)
        }
    };

    #endregion

    #region Setups and Teardowns

    [SetUp]
    public async Task Setup()
    {
        _connectionName = Helpers.UniqueString("kafka-reconcile-expand");
        _consumerName = Helpers.UniqueString("consumer-reconcile-expand");
        _serviceKey = Helpers.UniqueString("topic-reconcile-expand");
        _topicName = MqNaming.GetSafeName<ReconciliationExpandMessage>();

        // Ensure clean state
        await EnsureTopicDeletedAsync(_topicName);

        // Pre-create topic with initial config (simulating what a producer would create with connection defaults)
        await CreateTopicWithInitialConfigAsync(_topicName);
        await WaitForTopicCreatedAsync(_topicName);

        // Start consumer host - this should trigger reconciliation during initialization
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

                services.AddJsonConfiguration();
                services.ConfigureAsyncInitializers();

                services.AddMessageQueues(configuration)
                    .AddKafkaClientProviders()
                    .AddKafkaConsumers()
                    .AddConsumer<ReconciliationExpandMessage, ReconciliationExpandConsumer>(_consumerName)
                    .Build();
            },
            beforeStart: async (host) =>
            {
                await host.InvokeAsyncInitializers();
            },
            startHost: true
        );
    }

    [TearDown]
    public async Task TearDown()
    {
        await _testHost.DisposeAsync();
        await EnsureTopicDeletedAsync(_topicName);
    }

    #endregion

    #region Helper Methods

    private static async Task CreateTopicWithInitialConfigAsync(string topicName)
    {
        using var adminClient = CreateAdminClient();

        await adminClient.CreateTopicsAsync(
        [
            new TopicSpecification
            {
                Name = topicName,
                NumPartitions = InitialNumPartitions,
                ReplicationFactor = InitialReplicationFactor,
                Configs = new Dictionary<string, string>
                {
                    { "retention.ms", TimeSpan.FromDays(7).TotalMilliseconds.ToString(CultureInfo.InvariantCulture) },
                    { "retention.bytes", "-1" },
                    { "cleanup.policy", "delete" },
                    { "delete.retention.ms", TimeSpan.FromDays(1).TotalMilliseconds.ToString(CultureInfo.InvariantCulture) }
                }
            }
        ]);
    }

    private static async Task WaitForTopicCreatedAsync(string topicName)
    {
        using var adminClient = CreateAdminClient();
        var maxWait = TimeSpan.FromSeconds(30);
        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < maxWait)
        {
            var metadata = adminClient.GetMetadata(topicName, TimeSpan.FromSeconds(5));
            if (metadata.Topics.Any(t => t.Topic == topicName && t.Error.Code == ErrorCode.NoError))
            {
                return;
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"Topic '{topicName}' was not created within {maxWait.TotalSeconds}s");
    }

    private static async Task EnsureTopicDeletedAsync(string topicName)
    {
        using var adminClient = CreateAdminClient();

        try
        {
            await adminClient.DeleteTopicsAsync([topicName]);
            await TestContext.Out.WriteLineAsync($"Requested deletion of topic: {topicName}");
        }
        catch (DeleteTopicsException ex) when (ex.Results.All(r =>
            r.Error.Code == ErrorCode.UnknownTopicOrPart))
        {
            await TestContext.Out.WriteLineAsync($"Topic {topicName} does not exist");
            return;
        }
        catch (Exception ex)
        {
            await TestContext.Out.WriteLineAsync($"Delete request error (may be OK): {ex.Message}");
        }

        var maxWait = TimeSpan.FromSeconds(30);
        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < maxWait)
        {
            await Task.Delay(500);

            try
            {
                var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(5));
                var topicExists = metadata.Topics.Any(t =>
                    t.Topic == topicName &&
                    t.Error.Code == ErrorCode.NoError);

                if (!topicExists)
                {
                    await TestContext.Out.WriteLineAsync($"Topic {topicName} confirmed deleted");
                    return;
                }
            }
            catch
            {
                // Metadata query failed, continue waiting
            }
        }

        throw new TimeoutException($"Topic {topicName} was not deleted within {maxWait.TotalSeconds}s");
    }

    private static TopicMetadata? GetTopicMetadata(string topicName)
    {
        using var adminClient = CreateAdminClient();
        var metadata = adminClient.GetMetadata(topicName, TimeSpan.FromSeconds(10));
        return metadata.Topics.FirstOrDefault(t => t.Topic == topicName && t.Error.Code == ErrorCode.NoError);
    }

    private static async Task<DescribeConfigsResult?> GetTopicConfigAsync(string topicName)
    {
        using var adminClient = CreateAdminClient();

        var configResource = new ConfigResource
        {
            Type = ResourceType.Topic,
            Name = topicName
        };

        var results = await adminClient.DescribeConfigsAsync([configResource]);
        return results.FirstOrDefault();
    }

    private static IAdminClient CreateAdminClient()
    {
        var adminConfig = new AdminClientConfig
        {
            BootstrapServers = GlobalFixture.Kafka1.Instance.GetBootstrapAddress(),
            SecurityProtocol = SecurityProtocol.Plaintext
        };

        return new AdminClientBuilder(adminConfig).Build();
    }

    #endregion

    [Test]
    public void Reconciliation_WhenTopicExistsWithFewerPartitions_ThenPartitionsAreExpanded()
    {
        // Arrange
        var topicMetadata = GetTopicMetadata(_topicName);

        // Assert
        using var scope = new AssertionScope();

        topicMetadata.Should().NotBeNull("Topic should exist after reconciliation");
        topicMetadata!.Partitions.Should().HaveCount(ConsumerDesiredPartitions,
            $"Partitions should be expanded from {InitialNumPartitions} to {ConsumerDesiredPartitions}");
    }

    [Test]
    public async Task Reconciliation_WhenTopicExistsWithDifferentRetention_ThenRetentionIsUpdated()
    {
        // Arrange
        var expectedRetentionMs = TimeSpan.Parse(ConsumerRetentionTime).TotalMilliseconds.ToString("F0");

        // Act
        var configResult = await GetTopicConfigAsync(_topicName);

        // Assert
        using var scope = new AssertionScope();

        configResult.Should().NotBeNull("Topic config should be retrievable");

        var retentionConfig = configResult!.Entries.FirstOrDefault(e => e.Key == "retention.ms");
        retentionConfig.Should().NotBeNull("retention.ms config should exist");
        retentionConfig!.Value.Value.Should().Be(expectedRetentionMs,
            "retention.ms should be updated to consumer desired value");
    }

    [Test]
    public async Task Reconciliation_WhenTopicExistsWithDifferentRetentionBytes_ThenRetentionBytesAreUpdated()
    {
        // Act
        var configResult = await GetTopicConfigAsync(_topicName);

        // Assert
        using var scope = new AssertionScope();

        configResult.Should().NotBeNull("Topic config should be retrievable");

        var retentionBytesConfig = configResult!.Entries.FirstOrDefault(e => e.Key == "retention.bytes");
        retentionBytesConfig.Should().NotBeNull("retention.bytes config should exist");
        retentionBytesConfig!.Value.Value.Should().Be(ConsumerRetentionBytes.ToString(),
            "retention.bytes should be updated to consumer desired value");
    }

    [Test]
    public async Task Reconciliation_WhenTopicExistsWithDifferentDeleteRetention_ThenDeleteRetentionIsUpdated()
    {
        // Arrange
        var expectedDeleteRetentionMs = TimeSpan.Parse(ConsumerDeleteRetentionTime).TotalMilliseconds.ToString("F0");

        // Act
        var configResult = await GetTopicConfigAsync(_topicName);

        // Assert
        using var scope = new AssertionScope();

        configResult.Should().NotBeNull("Topic config should be retrievable");

        var deleteRetentionConfig = configResult!.Entries.FirstOrDefault(e => e.Key == "delete.retention.ms");
        deleteRetentionConfig.Should().NotBeNull("delete.retention.ms config should exist");
        deleteRetentionConfig!.Value.Value.Should().Be(expectedDeleteRetentionMs,
            "delete.retention.ms should be updated to consumer desired value");
    }
}

/// <summary>
/// Tests that verify the consumer initializer does NOT reduce partitions when the existing topic
/// has more partitions than the consumer desires, since Kafka does not support partition reduction.
/// Topic configuration should still be updated.
/// </summary>
[TestFixture]
[Category("Integration")]
[Category("TestContainers")]
public class KafkaTopicReconciliationPartitionReductionSkippedTests
{
    #region Constants

    private const int InitialNumPartitions = 5;
    private const short InitialReplicationFactor = 1;
    private const int ConsumerDesiredPartitions = 2;
    private const string ConsumerRetentionTime = "1.00:00:00";
    private const string ConsumerCleanupPolicy = "Compact";

    #endregion

    #region Fields

    private TestHost _testHost = null!;
    private string _connectionName = Helpers.UniqueString("kafka-reconcile-skip");
    private string _consumerName = Helpers.UniqueString("consumer-reconcile-skip");
    private string _serviceKey = Helpers.UniqueString("topic-reconcile-skip");
    private string _topicName = null!;

    #endregion

    #region Helper Classes

    public class ReconciliationSkipMessage
    {
        public string Data { get; init; } = string.Empty;
    }

    public class ReconciliationSkipConsumer(ILogger<ReconciliationSkipConsumer> logger)
    : IConsumer<ReconciliationSkipMessage>
    {
        public Task<bool> HandleMessageAsync(
            MessageEnvelop<ReconciliationSkipMessage> envelop,
            CancellationToken cancellationToken = default)
        {
            logger.ReceivedMessage(envelop.Message.Data);
            return Task.FromResult(true);
        }
    }

    #endregion

    #region Properties

    private object AppSettings => new
    {
        MessageQueues = new
        {
            Connections = new[]
            {
                new
                {
                    Name = _connectionName,
                    Type = "kafka",
                    ConnectionString = GlobalFixture.Kafka1.Instance.GetBootstrapAddress(),
                    ServiceKey = _serviceKey,
                    ErrorHandling = "NackAndDiscard"
                }
            },
            Consumers = new[]
            {
                new
                {
                    Name = _consumerName,
                    ConnectionName = _connectionName,
                    Parallelism = 1,
                    ErrorHandling = "NackAndDiscard",
                    Parameters = new
                    {
                        TopicConfiguration = new
                        {
                            NumPartitions = ConsumerDesiredPartitions,
                            ReplicationFactor = InitialReplicationFactor,
                            RetentionTime = ConsumerRetentionTime,
                            CleanUpPolicy = ConsumerCleanupPolicy
                        }
                    }
                }
            }
        },
        AsyncInitializer = new
        {
            Enabled = true,
            Timeout = TimeSpan.FromMinutes(2)
        }
    };

    #endregion

    #region Setups and Teardowns

    [SetUp]
    public async Task Setup()
    {
        _connectionName = Helpers.UniqueString("kafka-reconcile-skip");
        _consumerName = Helpers.UniqueString("consumer-reconcile-skip");
        _serviceKey = Helpers.UniqueString("topic-reconcile-skip");
        _topicName = MqNaming.GetSafeName<ReconciliationSkipMessage>();

        // Ensure clean state
        await EnsureTopicDeletedAsync(_topicName);

        // Pre-create topic with MORE partitions than consumer wants
        await CreateTopicWithInitialConfigAsync(_topicName);
        await WaitForTopicCreatedAsync(_topicName);

        // Start consumer host - reconciliation should skip partition reduction but update configs
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

                services.AddJsonConfiguration();
                services.ConfigureAsyncInitializers();

                services.AddMessageQueues(configuration)
                    .AddKafkaClientProviders()
                    .AddKafkaConsumers()
                    .AddConsumer<ReconciliationSkipMessage, ReconciliationSkipConsumer>(_consumerName)
                    .Build();
            },
            beforeStart: async (host) =>
            {
                await host.InvokeAsyncInitializers();
            },
            startHost: true
        );
    }

    [TearDown]
    public async Task TearDown()
    {
        await _testHost.DisposeAsync();
        await EnsureTopicDeletedAsync(_topicName);
    }

    #endregion

    #region Helper Methods

    private static async Task CreateTopicWithInitialConfigAsync(string topicName)
    {
        using var adminClient = CreateAdminClient();

        await adminClient.CreateTopicsAsync(
        [
            new TopicSpecification
            {
                Name = topicName,
                NumPartitions = InitialNumPartitions,
                ReplicationFactor = InitialReplicationFactor,
                Configs = new Dictionary<string, string>
                {
                    { "retention.ms", TimeSpan.FromDays(7).TotalMilliseconds.ToString(CultureInfo.InvariantCulture) },
                    { "retention.bytes", "-1" },
                    { "cleanup.policy", "delete" },
                    { "delete.retention.ms", TimeSpan.FromDays(1).TotalMilliseconds.ToString(CultureInfo.InvariantCulture) }
                }
            }
        ]);
    }

    private static async Task WaitForTopicCreatedAsync(string topicName)
    {
        using var adminClient = CreateAdminClient();
        var maxWait = TimeSpan.FromSeconds(30);
        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < maxWait)
        {
            var metadata = adminClient.GetMetadata(topicName, TimeSpan.FromSeconds(5));
            if (metadata.Topics.Any(t => t.Topic == topicName && t.Error.Code == ErrorCode.NoError))
            {
                return;
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"Topic '{topicName}' was not created within {maxWait.TotalSeconds}s");
    }

    private static async Task EnsureTopicDeletedAsync(string topicName)
    {
        using var adminClient = CreateAdminClient();

        try
        {
            await adminClient.DeleteTopicsAsync([topicName]);
            await TestContext.Out.WriteLineAsync($"Requested deletion of topic: {topicName}");
        }
        catch (DeleteTopicsException ex) when (ex.Results.All(r =>
            r.Error.Code == ErrorCode.UnknownTopicOrPart))
        {
            await TestContext.Out.WriteLineAsync($"Topic {topicName} does not exist");
            return;
        }
        catch (Exception ex)
        {
            await TestContext.Out.WriteLineAsync($"Delete request error (may be OK): {ex.Message}");
        }

        var maxWait = TimeSpan.FromSeconds(30);
        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < maxWait)
        {
            await Task.Delay(500);

            try
            {
                var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(5));
                var topicExists = metadata.Topics.Any(t =>
                    t.Topic == topicName &&
                    t.Error.Code == ErrorCode.NoError);

                if (!topicExists)
                {
                    await TestContext.Out.WriteLineAsync($"Topic {topicName} confirmed deleted");
                    return;
                }
            }
            catch
            {
                // Metadata query failed, continue waiting
            }
        }

        throw new TimeoutException($"Topic {topicName} was not deleted within {maxWait.TotalSeconds}s");
    }

    private static TopicMetadata? GetTopicMetadata(string topicName)
    {
        using var adminClient = CreateAdminClient();
        var metadata = adminClient.GetMetadata(topicName, TimeSpan.FromSeconds(10));
        return metadata.Topics.FirstOrDefault(t => t.Topic == topicName && t.Error.Code == ErrorCode.NoError);
    }

    private static async Task<DescribeConfigsResult?> GetTopicConfigAsync(string topicName)
    {
        using var adminClient = CreateAdminClient();

        var configResource = new ConfigResource
        {
            Type = ResourceType.Topic,
            Name = topicName
        };

        var results = await adminClient.DescribeConfigsAsync([configResource]);
        return results.FirstOrDefault();
    }

    private static IAdminClient CreateAdminClient()
    {
        var adminConfig = new AdminClientConfig
        {
            BootstrapServers = GlobalFixture.Kafka1.Instance.GetBootstrapAddress(),
            SecurityProtocol = SecurityProtocol.Plaintext
        };

        return new AdminClientBuilder(adminConfig).Build();
    }

    #endregion

    [Test]
    public void Reconciliation_WhenTopicExistsWithMorePartitions_ThenPartitionsAreNotReduced()
    {
        // Arrange
        var topicMetadata = GetTopicMetadata(_topicName);

        // Assert
        using var scope = new AssertionScope();

        topicMetadata.Should().NotBeNull("Topic should exist after reconciliation");
        topicMetadata!.Partitions.Should().HaveCount(InitialNumPartitions,
            $"Partitions should remain at {InitialNumPartitions} because Kafka does not support partition reduction");
    }

    [Test]
    public async Task Reconciliation_WhenPartitionReductionSkipped_ThenConfigIsStillUpdated()
    {
        // Arrange
        var expectedRetentionMs = TimeSpan.Parse(ConsumerRetentionTime).TotalMilliseconds.ToString("F0");

        // Act
        var configResult = await GetTopicConfigAsync(_topicName);

        // Assert
        using var scope = new AssertionScope();

        configResult.Should().NotBeNull("Topic config should be retrievable");

        var retentionConfig = configResult!.Entries.FirstOrDefault(e => e.Key == "retention.ms");
        retentionConfig.Should().NotBeNull("retention.ms config should exist");
        retentionConfig!.Value.Value.Should().Be(expectedRetentionMs,
            "retention.ms should still be updated even when partition reduction is skipped");

        var cleanupConfig = configResult.Entries.FirstOrDefault(e => e.Key == "cleanup.policy");
        cleanupConfig.Should().NotBeNull("cleanup.policy config should exist");
        cleanupConfig!.Value.Value.Should().Be("compact",
            "cleanup.policy should still be updated even when partition reduction is skipped");
    }
}
