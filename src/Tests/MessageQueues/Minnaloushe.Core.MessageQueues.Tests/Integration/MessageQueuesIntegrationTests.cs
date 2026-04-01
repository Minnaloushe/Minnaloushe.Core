using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Extensions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Kafka.Extensions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.RabbitMq.Extensions;
using Minnaloushe.Core.MessageQueues.Kafka.Consumers.Extensions;
using Minnaloushe.Core.MessageQueues.Kafka.Producers.Extensions;
using Minnaloushe.Core.MessageQueues.RabbitMq.Producers;
using Minnaloushe.Core.MessageQueues.Tests.HelperClasses;
using Minnaloushe.Core.Toolbox.AsyncInitializer.Extensions;
using Minnaloushe.Core.Toolbox.JsonConfiguration;
using Minnaloushe.Core.Toolbox.TestHelpers;

namespace Minnaloushe.Core.MessageQueues.Tests.Integration;

public class MessageQueuesIntegrationTests
{
    #region Constants

    private const string Producer1Name = "consumer1";
    private const string Producer2Name = "consumer2";

    #endregion

    #region Fields

    private TestHost _testHost;

    internal static readonly AsyncThresholdCollection<TestMessage> ReceivedMessages = new();

    #endregion

    #region Properties

#pragma warning disable IDE0037
    private static object AppSettings => new
    {
        MessageQueues = new
        {
            Connections = new object[]
            {
                new
                {
                    Name = "kafka-connection",
                    Type = "kafka",
                    Host = GlobalFixture.Kafka.Host,
                    Port = GlobalFixture.Kafka.Port,
                    Username = GlobalFixture.Kafka.Username,
                    Password = GlobalFixture.Kafka.Password,
                    ServiceKey = "test-topic",
                },
                new
                {
                    Name = "rabbit-connection",
                    Type = "rabbitmq",
                    ConnectionString = "",
                    ServiceKey = "test-queue",
                    Host = GlobalFixture.RabbitMq.Host,
                    Port = GlobalFixture.RabbitMq.Port,
                    Username = GlobalFixture.RabbitMq.Username,
                    Password = GlobalFixture.RabbitMq.Password
                }
            },
            Consumers = new object[]
            {
                new
                {
                    Name = "test-consumer1",
                    ConnectionName = "kafka-connection",
                    Parallelism = 1,
                    ErrorHandling = "DeadLetter",
                    Parameters = new Dictionary<string, string>
                    {
                        ["NumPartitions"] = "1",
                        ["ReplicationFactor"] = "1"
                    }
                },
                new
                {
                    Name = "test-consumer2",
                    ConnectionName = "rabbit-connection",
                    Parallelism = 1,
                    ErrorHandling = "DeadLetter"
                },
            }
        },
        AsyncInitializer = new
        {
            Enabled = true,
            Timeout = TimeSpan.FromMinutes(2)
        }
    };
#pragma warning restore IDE0037

    #endregion

    #region Setups and Teardowns

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        // Build test host without starting it
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

                // Register consumer and producer using simple client providers
                services.AddMessageQueues(configuration)

                    .AddRabbitMqClientProviders()
                    .AddRabbitMqConsumers()
                    .AddRabbitMqProducers()

                    .AddKafkaClientProviders()
                    .AddKafkaConsumers()
                    .AddKafkaProducers()

                    .AddConsumer<TestMessage, TestIntegrationConsumer>("test-consumer1")
                    .AddConsumer<TestMessage, TestIntegrationConsumer>("test-consumer2")

                    .AddProducer<TestMessage>("rabbit-connection", Producer1Name)
                    .AddProducer<TestMessage>("kafka-connection", Producer2Name)
                    .Build();
            },
            beforeStart: async host =>
            {

                await host.InvokeAsyncInitializers();
            },
            startHost: true
        );

    }

    // Tests should be outside of setup/teardown regions per test fixture pattern

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _testHost.DisposeAsync();
    }

    #endregion

    #region Tests

    [Test]
    public async Task MessageQueues_WhenPublishingToKafkaAndRabbit_ThenBothShouldBeReceived()
    {
        // Arrange
        var producer1 = _testHost.Services.GetRequiredKeyedService<IProducer<TestMessage>>(Producer1Name);
        var producer2 = _testHost.Services.GetRequiredKeyedService<IProducer<TestMessage>>(Producer2Name);

        // Act
        await producer1.PublishAsync(new TestMessage { Data = "Message to rabbit connection" });
        await producer2.PublishAsync(new TestMessage { Data = "Message to kafka connection" });

        // Assert
        await ReceivedMessages.WaitUntilCountAtLeastAsync(2, TimeSpan.FromSeconds(10));

        var snapshot = ReceivedMessages.GetSnapshot();

        snapshot.Count.Should().Be(2, "Both messages from rabbit and kafka should be received");
    }

    #endregion
}