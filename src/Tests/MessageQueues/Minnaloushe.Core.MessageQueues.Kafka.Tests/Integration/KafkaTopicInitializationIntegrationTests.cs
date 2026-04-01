using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.ClientProviders.Kafka.Options;
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
/// Tests that verify topics created by consumers use parameters from the connection configuration
/// when no consumer-level parameters are specified.
/// </summary>
[TestFixture]
[Category("Integration")]
[Category("TestContainers")]
public class KafkaConsumerTopicInitializationFromConnectionTests
{
    #region Constants

    private const int ExpectedNumPartitions = 5;
    private const short ExpectedReplicationFactor = 1;
    private const string ExpectedRetentionTime = "3.00:00:00"; // 3 days
    private const string ExpectedCleanupPolicy = "Delete";

    #endregion

    #region Fields

    private TestHost _testHost = null!;
    private string _connectionName = Helpers.UniqueString("kafka-conn-params");
    private string _consumerName = Helpers.UniqueString("consumer-conn-params");
    private string _serviceKey = Helpers.UniqueString("topic-conn-params");

    #endregion

    #region Helper Classes

    public class ConnectionParamsTestMessage
    {
        public string Data { get; init; } = string.Empty;
    }

    public class ConnectionParamsTestConsumer(ILogger<ConnectionParamsTestConsumer> logger)
        : IConsumer<ConnectionParamsTestMessage>
    {
        public Task<bool> HandleMessageAsync(
            MessageEnvelop<ConnectionParamsTestMessage> envelop,
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
                    ErrorHandling = "NackAndDiscard",
                    Parameters = new
                    {
                        TopicConfiguration = new
                        {
                            NumPartitions = ExpectedNumPartitions,
                            ReplicationFactor = ExpectedReplicationFactor,
                            RetentionTime = ExpectedRetentionTime,
                            CleanUpPolicy = ExpectedCleanupPolicy
                        }
                    }
                }
            },
            Consumers = new[]
            {
                new
                {
                    Name = _consumerName,
                    ConnectionName = _connectionName,
                    Parallelism = 1,
                    ErrorHandling = "NackAndDiscard"
                    // No Parameters - should inherit from connection
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
        var topicName = MqNaming.GetSafeName<ConnectionParamsTestMessage>();

        _connectionName = Helpers.UniqueString("kafka-conn-params");
        _consumerName = Helpers.UniqueString("consumer-conn-params");
        _serviceKey = Helpers.UniqueString("topic-conn-params");

        // Delete existing topic and wait for Kafka to fully remove it
        await EnsureTopicDeletedAsync(topicName);

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
                    .AddConsumer<ConnectionParamsTestMessage, ConnectionParamsTestConsumer>(_consumerName)
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
        await EnsureTopicDeletedAsync(GetExpectedTopicName());
    }

    private static async Task EnsureTopicDeletedAsync(string topicName)
    {
        using var adminClient = CreateAdminClient();

        // Always attempt deletion - ignore errors if topic doesn't exist
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

        // Wait for topic to be fully deleted - Kafka deletion is async
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

    #endregion

    #region Helper Methods

    private static string GetExpectedTopicName()
    {
        return MqNaming.GetSafeName<ConnectionParamsTestMessage>();
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
    public async Task ConsumerOptions_WhenResolved_ThenShouldHaveConnectionParameters()
    {
        // Arrange - Verify the options are loaded correctly
        var optionsMonitor = _testHost.Services.GetRequiredService<IOptionsMonitor<MessageQueueOptions>>();
        var options = optionsMonitor.Get(_consumerName);

        // Act & Assert
        using var scope = new AssertionScope();

        options.Parameters.Should().NotBeEmpty("Parameters should be loaded from connection config");
        options.Parameters.Should().ContainKey("TopicConfiguration", "TopicConfiguration should exist in Parameters");

        // Log actual parameters for debugging
        await TestContext.Out.WriteLineAsync($"Parameters count: {options.Parameters.Count}");
        foreach (var kvp in options.Parameters)
        {
            await TestContext.Out.WriteLineAsync($"  {kvp.Key}: {kvp.Value}");
        }

        // Also verify the deserialized TopicConfiguration
        var kafkaOptions = options.ToClientOptions();
        await TestContext.Out.WriteLineAsync($"TopicConfiguration.NumPartitions: {kafkaOptions.Parameters.TopicConfiguration.NumPartitions}");
        await TestContext.Out.WriteLineAsync($"TopicConfiguration.RetentionTime: {kafkaOptions.Parameters.TopicConfiguration.RetentionTime}");
        await TestContext.Out.WriteLineAsync($"TopicConfiguration.CleanUpPolicy: {kafkaOptions.Parameters.TopicConfiguration.CleanUpPolicy}");

        kafkaOptions.Parameters.TopicConfiguration.NumPartitions.Should().Be(ExpectedNumPartitions,
            "TopicConfiguration should deserialize NumPartitions from Parameters");
        kafkaOptions.Parameters.TopicConfiguration.RetentionTime.Should().Be(TimeSpan.Parse(ExpectedRetentionTime),
            "TopicConfiguration should deserialize RetentionTime from Parameters");
        kafkaOptions.Parameters.TopicConfiguration.CleanUpPolicy.Should().Be(
            CleanUpPolicy.Delete,
            "TopicConfiguration should deserialize CleanUpPolicy from Parameters");
    }

    [Test]
    public void ConsumerTopic_WhenCreated_ThenShouldUseConnectionParameters()
    {
        // Arrange
        var expectedTopicName = GetExpectedTopicName();

        // Act
        var topicMetadata = GetTopicMetadata(expectedTopicName);

        // Assert
        using var scope = new AssertionScope();

        topicMetadata.Should().NotBeNull("Topic should exist");
        topicMetadata.Partitions.Should().HaveCount(ExpectedNumPartitions,
            "Topic should have partition count from connection parameters");
    }

    [Test]
    public async Task ConsumerTopic_WhenCreated_ThenShouldHaveCorrectRetentionConfig()
    {
        // Arrange
        var expectedTopicName = GetExpectedTopicName();
        var expectedRetentionMs = TimeSpan.Parse(ExpectedRetentionTime).TotalMilliseconds.ToString("F0");

        // Act
        var configResult = await GetTopicConfigAsync(expectedTopicName);

        // Assert
        using var scope = new AssertionScope();

        configResult.Should().NotBeNull("Topic config should be retrievable");

        var retentionConfig = configResult.Entries.FirstOrDefault(e => e.Key == "retention.ms");
        retentionConfig.Should().NotBeNull("retention.ms config should exist");
        retentionConfig.Value.Value.Should().Be(expectedRetentionMs,
            "Retention should match connection parameters");
    }

    [Test]
    public async Task ConsumerTopic_WhenCreated_ThenShouldHaveCorrectCleanupPolicy()
    {
        // Arrange
        var expectedTopicName = GetExpectedTopicName();

        // Act
        var configResult = await GetTopicConfigAsync(expectedTopicName);

        // Assert
        using var scope = new AssertionScope();

        configResult.Should().NotBeNull("Topic config should be retrievable");

        var cleanupConfig = configResult.Entries.FirstOrDefault(e => e.Key == "cleanup.policy");
        cleanupConfig.Should().NotBeNull("cleanup.policy config should exist");
        cleanupConfig.Value.Value.Should().Be("delete",
            "Cleanup policy should match connection parameters");
    }
}

/// <summary>
/// Tests that verify topics created by consumers use parameters from the consumer configuration
/// when consumer-level parameters are specified (overriding connection parameters).
/// </summary>
[TestFixture]
[Category("Integration")]
[Category("TestContainers")]
public class KafkaConsumerTopicInitializationWithOverrideTests
{
    #region Constants

    // Connection parameters (should be overridden)
    private const int ConnectionNumPartitions = 10;
    private const string ConnectionRetentionTime = "7.00:00:00"; // 7 days
    private const string ConnectionCleanupPolicy = "Delete";

    // Consumer override parameters (should take priority)
    private const int ConsumerNumPartitions = 3;
    private const string ConsumerRetentionTime = "1.00:00:00"; // 1 day
    private const string ConsumerCleanupPolicy = "Compact";

    #endregion

    #region Fields

    private TestHost _testHost = null!;
    private readonly string _connectionName = Helpers.UniqueString("kafka-conn-override");
    private readonly string _consumerName = Helpers.UniqueString("consumer-override");
    private readonly string _serviceKey = Helpers.UniqueString("topic-override");

    #endregion

    #region Helper Classes

    public class OverrideParamsTestMessage
    {
        public string Data { get; init; } = string.Empty;
    }

    public class OverrideParamsTestConsumer(ILogger<OverrideParamsTestConsumer> logger)
        : IConsumer<OverrideParamsTestMessage>
    {
        public Task<bool> HandleMessageAsync(
            MessageEnvelop<OverrideParamsTestMessage> envelop,
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
                    ErrorHandling = "NackAndDiscard",
                    Parameters = new
                    {
                        TopicConfiguration = new
                        {
                            NumPartitions = ConnectionNumPartitions,
                            ReplicationFactor = (short)1,
                            RetentionTime = ConnectionRetentionTime,
                            CleanUpPolicy = ConnectionCleanupPolicy
                        }
                    }
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
                            NumPartitions = ConsumerNumPartitions,
                            ReplicationFactor = (short)1,
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
    public async Task SetUp()
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
                    builder.SetMinimumLevel(LogLevel.Debug);
                });

                services.AddJsonConfiguration();
                services.ConfigureAsyncInitializers();

                services.AddMessageQueues(configuration)
                    .AddKafkaClientProviders()
                    .AddKafkaConsumers()
                    .AddConsumer<OverrideParamsTestMessage, OverrideParamsTestConsumer>(_consumerName)
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
        await EnsureTopicDeletedAsync(GetExpectedTopicName());
        await _testHost.DisposeAsync();
    }

    #endregion

    #region Helper Methods

    private static async Task EnsureTopicDeletedAsync(string topicName)
    {
        using var adminClient = CreateAdminClient();

        // Always attempt deletion - ignore errors if topic doesn't exist
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

        // Wait for topic to be fully deleted - Kafka deletion is async
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


    private static string GetExpectedTopicName()
    {
        return MqNaming.GetSafeName<OverrideParamsTestMessage>();
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
    public void ConsumerOptions_WhenResolved_ThenShouldHaveConsumerParametersOverridingConnection()
    {
        // Arrange - Verify the options are loaded correctly with consumer overrides
        var optionsMonitor = _testHost.Services.GetRequiredService<IOptionsMonitor<MessageQueueOptions>>();
        var options = optionsMonitor.Get(_consumerName);

        // Act & Assert
        using var scope = new AssertionScope();

        options.Parameters.Should().NotBeEmpty("Parameters should be loaded");
        options.Parameters.Should().ContainKey("TopicConfiguration", "TopicConfiguration should exist in Parameters");

        // Verify the deserialized TopicConfiguration uses consumer values (not connection)
        var kafkaOptions = options.ToClientOptions();

        kafkaOptions.Parameters.TopicConfiguration.NumPartitions.Should().Be(ConsumerNumPartitions,
            "TopicConfiguration should use NumPartitions from consumer parameters (not connection)");
        kafkaOptions.Parameters.TopicConfiguration.RetentionTime.Should().Be(TimeSpan.Parse(ConsumerRetentionTime),
            "TopicConfiguration should use RetentionTime from consumer parameters (not connection)");
        kafkaOptions.Parameters.TopicConfiguration.CleanUpPolicy.Should().Be(CleanUpPolicy.Compact,
            "TopicConfiguration should use CleanUpPolicy from consumer parameters (not connection)");
    }

    [Test]
    public void ConsumerTopic_WhenCreatedWithOverride_ThenShouldUseConsumerParameters()
    {
        // Arrange
        var expectedTopicName = GetExpectedTopicName();

        // Act
        var topicMetadata = GetTopicMetadata(expectedTopicName);

        // Assert
        using var scope = new AssertionScope();

        topicMetadata.Should().NotBeNull("Topic should exist");
        topicMetadata.Partitions.Should().HaveCount(ConsumerNumPartitions,
            "Topic should have partition count from consumer parameters (not connection)");
    }

    [Test]
    public async Task ConsumerTopic_WhenCreatedWithOverride_ThenShouldHaveConsumerRetentionConfig()
    {
        // Arrange
        var expectedTopicName = GetExpectedTopicName();
        var expectedRetentionMs = TimeSpan.Parse(ConsumerRetentionTime).TotalMilliseconds.ToString("F0");

        // Act
        var configResult = await GetTopicConfigAsync(expectedTopicName);

        // Assert
        using var scope = new AssertionScope();

        configResult.Should().NotBeNull("Topic config should be retrievable");

        var retentionConfig = configResult.Entries.FirstOrDefault(e => e.Key == "retention.ms");
        retentionConfig.Should().NotBeNull("retention.ms config should exist");
        retentionConfig.Value.Value.Should().Be(expectedRetentionMs,
            "Retention should match consumer parameters (not connection)");
    }

    [Test]
    public async Task ConsumerTopic_WhenCreatedWithOverride_ThenShouldHaveConsumerCleanupPolicy()
    {
        // Arrange
        var expectedTopicName = GetExpectedTopicName();

        // Act
        var configResult = await GetTopicConfigAsync(expectedTopicName);

        // Assert
        using var scope = new AssertionScope();

        configResult.Should().NotBeNull("Topic config should be retrievable");

        var cleanupConfig = configResult.Entries.FirstOrDefault(e => e.Key == "cleanup.policy");
        cleanupConfig.Should().NotBeNull("cleanup.policy config should exist");
        cleanupConfig.Value.Value.Should().Be("compact",
            "Cleanup policy should match consumer parameters (not connection)");
    }
}

/// <summary>
/// Tests that verify topics created by producers use parameters from the connection configuration.
/// </summary>
[TestFixture]
[Category("Integration")]
[Category("TestContainers")]
public class KafkaProducerTopicInitializationTests
{
    #region Constants

    private const int ExpectedNumPartitions = 4;
    private const short ExpectedReplicationFactor = 1;
    private const string ExpectedRetentionTime = "2.00:00:00"; // 2 days
    private const string ExpectedCleanupPolicy = "CompactAndDelete"; // Using Delete to avoid key requirement for compact topics

    #endregion

    #region Fields

    private TestHost _testHost = null!;
    private string _connectionName = Helpers.UniqueString("kafka-producer-params");
    private string _serviceKey = Helpers.UniqueString("topic-producer-params");

    #endregion

    #region Helper Classes

    public class ProducerParamsTestMessage
    {
        public string Data { get; init; } = string.Empty;
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
                    ErrorHandling = "NackAndDiscard",
                    Parameters = new
                    {
                        TopicConfiguration = new
                        {
                            NumPartitions = ExpectedNumPartitions,
                            ReplicationFactor = ExpectedReplicationFactor,
                            RetentionTime = ExpectedRetentionTime,
                            CleanUpPolicy = ExpectedCleanupPolicy
                        }
                    }
                }
            },
            Consumers = Array.Empty<object>()
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
        var topicName = MqNaming.GetSafeName<ProducerParamsTestMessage>();

        _connectionName = Helpers.UniqueString("kafka-producer-params");
        _serviceKey = Helpers.UniqueString("topic-producer-params");

        // Delete existing topic and wait for Kafka to fully remove it
        await EnsureTopicDeletedAsync(topicName);

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
                        .AddKafkaProducers()
                        .AddProducer<ProducerParamsTestMessage>(_connectionName)
                        .Build();
                },
                beforeStart: async (host) =>
                {
                    await host.InvokeAsyncInitializers();
                },
                startHost: true
            );

        // Trigger topic creation by publishing a message
        var producer = _testHost.Services.GetRequiredService<IProducer<ProducerParamsTestMessage>>();
        await producer.PublishAsync(new ProducerParamsTestMessage { Data = "trigger-topic-creation" }, key: Guid.NewGuid().ToString("N"));

        // Wait for topic creation to complete
    }

    [TearDown]
    public async Task TearDown()
    {
        await _testHost.DisposeAsync();

        await EnsureTopicDeletedAsync(GetExpectedTopicName());
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

    #endregion

    #region Helper Methods

    private static string GetExpectedTopicName()
    {
        return MqNaming.GetSafeName<ProducerParamsTestMessage>();
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
    public void ProducerOptions_WhenResolved_ThenShouldHaveConnectionParameters()
    {
        // Arrange - Verify the options are loaded correctly for producer
        var optionsMonitor = _testHost.Services.GetRequiredService<IOptionsMonitor<MessageQueueOptions>>();
        var options = optionsMonitor.Get(_connectionName);

        // Act & Assert
        using var scope = new AssertionScope();

        options.Parameters.Should().NotBeEmpty("Parameters should be loaded from connection config");
        options.Parameters.Should().ContainKey("TopicConfiguration", "TopicConfiguration should exist in Parameters");

        // Verify the deserialized TopicConfiguration
        var kafkaOptions = options.ToClientOptions();

        kafkaOptions.Parameters.TopicConfiguration.NumPartitions.Should().Be(ExpectedNumPartitions,
            "TopicConfiguration should deserialize NumPartitions from Parameters");
        kafkaOptions.Parameters.TopicConfiguration.RetentionTime.Should().Be(TimeSpan.Parse(ExpectedRetentionTime),
            "TopicConfiguration should deserialize RetentionTime from Parameters");
        kafkaOptions.Parameters.TopicConfiguration.CleanUpPolicy.Should().Be(CleanUpPolicy.CompactAndDelete,
            "TopicConfiguration should deserialize CleanUpPolicy from Parameters");
    }

    [Test]
    public void ProducerTopic_WhenCreatedOnDemand_ThenShouldUseConnectionParameters()
    {
        // Arrange
        var expectedTopicName = GetExpectedTopicName();

        // Act
        var topicMetadata = GetTopicMetadata(expectedTopicName);

        // Assert
        using var scope = new AssertionScope();

        topicMetadata.Should().NotBeNull("Topic should exist after publishing");
        topicMetadata.Partitions.Should().HaveCount(ExpectedNumPartitions,
            "Topic should have partition count from connection parameters");
    }

    [Test]
    public async Task ProducerTopic_WhenCreatedOnDemand_ThenShouldHaveCorrectRetentionConfig()
    {
        // Arrange
        var expectedTopicName = GetExpectedTopicName();
        var expectedRetentionMs = TimeSpan.Parse(ExpectedRetentionTime).TotalMilliseconds.ToString("F0");

        // Act
        var configResult = await GetTopicConfigAsync(expectedTopicName);

        // Assert
        using var scope = new AssertionScope();

        configResult.Should().NotBeNull("Topic config should be retrievable");

        var retentionConfig = configResult.Entries.FirstOrDefault(e => e.Key == "retention.ms");
        retentionConfig.Should().NotBeNull("retention.ms config should exist");
        retentionConfig.Value.Value.Should().Be(expectedRetentionMs,
            "Retention should match connection parameters");
    }

    [Test]
    public async Task ProducerTopic_WhenCreatedOnDemand_ThenShouldHaveCorrectCleanupPolicy()
    {
        // Arrange
        var expectedTopicName = GetExpectedTopicName();

        // Act
        var configResult = await GetTopicConfigAsync(expectedTopicName);

        // Assert
        using var scope = new AssertionScope();

        configResult.Should().NotBeNull("Topic config should be retrievable");

        var cleanupConfig = configResult.Entries.FirstOrDefault(e => e.Key == "cleanup.policy");
        cleanupConfig.Should().NotBeNull("cleanup.policy config should exist");
        cleanupConfig.Value.Value.Should().Contain("delete",
            "Cleanup policy should match connection parameters")
            .And.Contain("compact",
            "Cleanup policy should match connection parameters");
    }
}
