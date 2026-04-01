using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Minnaloushe.Core.ClientProviders.Abstractions;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Extensions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Models;
using Minnaloushe.Core.MessageQueues.DependencyInjection.RabbitMq.Extensions;
using Minnaloushe.Core.MessageQueues.RabbitMq.Producers;
using Minnaloushe.Core.Tests.Helpers;
using Minnaloushe.Core.Toolbox.AsyncInitializer.Extensions;
using Minnaloushe.Core.Toolbox.JsonConfiguration;
using Minnaloushe.Core.Toolbox.TestHelpers;
using RabbitMQ.Client;

namespace Minnaloushe.Core.MessageQueue.RabbitMq.Tests.Integration;

[TestFixture]
[Category("Integration")]
[Category("TestContainers")]
public class RabbitMqRegistrationRoutinesTests
{
    private readonly string _connection1Name = Helpers.UniqueString("conn1");
    private readonly string _connection2Name = Helpers.UniqueString("conn2");
    private readonly string _consumer1Name = Helpers.UniqueString("consumer1");
    private readonly string _consumer2Name = Helpers.UniqueString("consumer2");
    private readonly string _producer1Name = Helpers.UniqueString("producer1");
    private readonly string _producer2Name = Helpers.UniqueString("producer2");

    private static async Task<TestHost> InitHost(object appSettings, Action<MessageQueueBuilder>? setupQueues = null, bool startHost = false)
    {
        return await TestHost.Build(
            configureConfiguration: cfg =>
            {
                cfg.AddConfiguration(appSettings);
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
                var builder = services.AddMessageQueues(configuration);

                setupQueues?.Invoke(builder);
            },
            beforeStart: async (host) =>
            {
                await host.InvokeAsyncInitializers();
            },
            startHost: startHost
        );
    }

    [Test]
    public async Task AddMessageQueues_WhenCreatedWithoutMessageQueueConfiguration_ThenShouldSucceed()
    {
        var appSettings = MqHelpers.CreateAppSettings([], []);

        await using var sut = await InitHost(appSettings);

        sut.Should().NotBeNull();
    }

    [Test]
    public async Task AddMessageQueues_WhenOneConnectionWithoutConsumersOrProducers_ThenShouldRegisterClientProviders()
    {
        var appSettings = MqHelpers.CreateAppSettings(
            [MqHelpers.CreateConnection(_connection1Name, "rabbit", container: GlobalFixture.RabbitMqInstance1)],
            []);

        await using var sut = await InitHost(appSettings, builder =>
        {
            builder.AddRabbitMqClientProviders().Build();
        });

        var connectionProvider = sut.Services.GetKeyedService<IClientProvider<IConnection>>(_connection1Name);

        connectionProvider.Should().NotBeNull();
    }

    [Test]
    public async Task AddMessageQueues_WhenTwoConnectionsWithoutConsumersOrProducers_ThenShouldRegisterBothClientProviders()
    {
        var appSettings = MqHelpers.CreateAppSettings(
            [
                MqHelpers.CreateConnection(_connection1Name, "rabbit", container: GlobalFixture.RabbitMqInstance1),
                MqHelpers.CreateConnection(_connection2Name, "rabbit", container: GlobalFixture.RabbitMqInstance2)
            ],
            []);

        await using var sut = await InitHost(appSettings, builder =>
        {
            builder.AddRabbitMqClientProviders().Build();
        });

        var connectionProvider1 = sut.Services.GetKeyedService<IClientProvider<IConnection>>(_connection1Name);
        var connectionProvider2 = sut.Services.GetKeyedService<IClientProvider<IConnection>>(_connection2Name);

        connectionProvider1.Should().NotBeNull();
        connectionProvider2.Should().NotBeNull();
        connectionProvider1.Should().NotBeSameAs(connectionProvider2);
    }

    [Test]
    public async Task AddMessageQueues_WhenOneConnectionWithOneProducer_ThenShouldRegisterProducer()
    {
        var appSettings = MqHelpers.CreateAppSettings(
            [MqHelpers.CreateConnection(_connection1Name, "rabbit", container: GlobalFixture.RabbitMqInstance1)],
            []);

        await using var sut = await InitHost(appSettings, builder =>
        {
            builder.AddRabbitMqClientProviders()
                .AddRabbitMqProducers()
                .AddProducer<TestMessage1>(_connection1Name, _producer1Name)
                .Build();
        });

        var producer = sut.Services.GetKeyedService<IProducer<TestMessage1>>(_producer1Name);

        producer.Should().NotBeNull();
    }

    [Test]
    public async Task AddMessageQueues_WhenProducerIsRegistered_ThenShouldRegisterProducerProviderPerProducerKey()
    {
        var appSettings = MqHelpers.CreateAppSettings(
            [MqHelpers.CreateConnection(_connection1Name, "rabbit", container: GlobalFixture.RabbitMqInstance1)],
            []);

        await using var sut = await InitHost(appSettings, builder =>
        {
            builder.AddRabbitMqClientProviders()
                .AddRabbitMqProducers()
                .AddProducer<TestMessage1>(_connection1Name, _producer1Name)
                .Build();
        });

        var producerClientProvider = sut.Services.GetKeyedService<IClientProvider<IConnection>>(_producer1Name);
        var connectionClientProvider = sut.Services.GetKeyedService<IClientProvider<IConnection>>(_connection1Name);

        producerClientProvider.Should().NotBeNull();
        connectionClientProvider.Should().NotBeNull();
        producerClientProvider.Should().BeSameAs(connectionClientProvider);
    }

    [Test]
    public async Task AddMessageQueues_WhenOneConnectionWithTwoProducers_ThenShouldRegisterBothProducers()
    {
        var appSettings = MqHelpers.CreateAppSettings(
            [MqHelpers.CreateConnection(_connection1Name, "rabbit", container: GlobalFixture.RabbitMqInstance1)],
            []);

        await using var sut = await InitHost(appSettings, builder =>
        {
            builder.AddRabbitMqClientProviders()
                .AddRabbitMqProducers()
                .AddProducer<TestMessage1>(_connection1Name, _producer1Name)
                .AddProducer<TestMessage2>(_connection1Name, _producer2Name)
                .Build();
        });

        var producer1 = sut.Services.GetKeyedService<IProducer<TestMessage1>>(_producer1Name);
        var producer2 = sut.Services.GetKeyedService<IProducer<TestMessage2>>(_producer2Name);

        producer1.Should().NotBeNull();
        producer2.Should().NotBeNull();
    }

    [Test]
    public async Task AddMessageQueues_WhenTwoConnectionsWithOneProducerEach_ThenShouldRegisterBothProducers()
    {
        var appSettings = MqHelpers.CreateAppSettings(
            [
                MqHelpers.CreateConnection(_connection1Name, "rabbit", container: GlobalFixture.RabbitMqInstance1),
                MqHelpers.CreateConnection(_connection2Name, "rabbit", container: GlobalFixture.RabbitMqInstance2)
            ],
            []);

        await using var sut = await InitHost(appSettings, builder =>
        {
            builder.AddRabbitMqClientProviders()
                .AddRabbitMqProducers()
                .AddProducer<TestMessage1>(_connection1Name, _producer1Name)
                .AddProducer<TestMessage2>(_connection2Name, _producer2Name)
                .Build();
        });

        var producer1 = sut.Services.GetKeyedService<IProducer<TestMessage1>>(_producer1Name);
        var producer2 = sut.Services.GetKeyedService<IProducer<TestMessage2>>(_producer2Name);
        var clientProvider1 = sut.Services.GetKeyedService<IClientProvider<IConnection>>(_producer1Name);
        var clientProvider2 = sut.Services.GetKeyedService<IClientProvider<IConnection>>(_producer2Name);

        producer1.Should().NotBeNull();
        producer2.Should().NotBeNull();
        clientProvider1.Should().NotBeNull();
        clientProvider2.Should().NotBeNull();
    }

    [Test]
    public async Task AddMessageQueues_WhenOneConnectionWithOneConsumer_ThenShouldRegisterConsumer()
    {
        var appSettings = MqHelpers.CreateAppSettings(
            [MqHelpers.CreateConnection(_connection1Name, "rabbit", container: GlobalFixture.RabbitMqInstance1)],
            [MqHelpers.CreateConsumer(_consumer1Name, _connection1Name)]);

        await using var sut = await InitHost(appSettings, builder =>
        {
            builder.AddRabbitMqClientProviders()
                .AddRabbitMqConsumers()
                .AddConsumer<TestMessage1, TestConsumer1>(_consumer1Name)
                .Build();
        });

        var scopeFactory = sut.Services.GetRequiredService<IServiceScopeFactory>();
        using var serviceScope = scopeFactory.CreateScope();

        var consumerInitializer = serviceScope.ServiceProvider.GetKeyedService<IConsumerInitializer>(_consumer1Name);
        var messageEngine = serviceScope.ServiceProvider.GetService<IMessageEngineFactory<TestMessage1?, IConnection>>();
        var consumer = serviceScope.ServiceProvider.GetService<IConsumer<TestMessage1>>();

        consumerInitializer.Should().NotBeNull();
        messageEngine.Should().NotBeNull();
        consumer.Should().NotBeNull();
    }

    [Test]
    public async Task AddMessageQueues_WhenOneConnectionWithTwoConsumers_ThenShouldRegisterBothConsumers()
    {
        var appSettings = MqHelpers.CreateAppSettings(
            [MqHelpers.CreateConnection(_connection1Name, "rabbit", container: GlobalFixture.RabbitMqInstance1)],
            [
                MqHelpers.CreateConsumer(_consumer1Name, _connection1Name),
                MqHelpers.CreateConsumer(_consumer2Name, _connection1Name)
            ]);

        await using var sut = await InitHost(appSettings, builder =>
        {
            builder.AddRabbitMqClientProviders()
                .AddRabbitMqConsumers()
                .AddConsumer<TestMessage1, TestConsumer1>(_consumer1Name)
                .AddConsumer<TestMessage2, TestConsumer2>(_consumer2Name)
                .Build();
        });
        var scopeFactory = sut.Services.GetRequiredService<IServiceScopeFactory>();
        using var serviceScope = scopeFactory.CreateScope();

        var consumerInitializer1 = serviceScope.ServiceProvider.GetKeyedService<IConsumerInitializer>(_consumer1Name);
        var consumerInitializer2 = serviceScope.ServiceProvider.GetKeyedService<IConsumerInitializer>(_consumer2Name);

        var consumer1 = serviceScope.ServiceProvider.GetService<IConsumer<TestMessage1>>();
        var consumer2 = serviceScope.ServiceProvider.GetService<IConsumer<TestMessage2>>();

        consumerInitializer1.Should().NotBeNull();
        consumerInitializer2.Should().NotBeNull();
        consumer1.Should().NotBeNull();
        consumer2.Should().NotBeNull();
        consumerInitializer1.Should().NotBeSameAs(consumerInitializer2);
    }

    [Test]
    public async Task AddMessageQueues_WhenTwoConnectionsWithOneConsumerEach_ThenShouldRegisterBothConsumers()
    {
        var appSettings = MqHelpers.CreateAppSettings(
            [
                MqHelpers.CreateConnection(_connection1Name, "rabbit", container : GlobalFixture.RabbitMqInstance1),
                MqHelpers.CreateConnection(_connection2Name, "rabbit", container: GlobalFixture.RabbitMqInstance2)
            ],
            [
                MqHelpers.CreateConsumer(_consumer1Name, _connection1Name),
                MqHelpers.CreateConsumer(_consumer2Name, _connection2Name)
            ]);

        await using var sut = await InitHost(appSettings, builder =>
        {
            builder.AddRabbitMqClientProviders()
                .AddRabbitMqConsumers()
                .AddConsumer<TestMessage1, TestConsumer1>(_consumer1Name)
                .AddConsumer<TestMessage2, TestConsumer2>(_consumer2Name)
                .Build();
        });

        var consumerInitializer1 = sut.Services.GetKeyedService<IConsumerInitializer>(_consumer1Name);
        var consumerInitializer2 = sut.Services.GetKeyedService<IConsumerInitializer>(_consumer2Name);
        var clientProvider1 = sut.Services.GetKeyedService<IClientProvider<IConnection>>(_consumer1Name);
        var clientProvider2 = sut.Services.GetKeyedService<IClientProvider<IConnection>>(_consumer2Name);

        consumerInitializer1.Should().NotBeNull();
        consumerInitializer2.Should().NotBeNull();
        clientProvider1.Should().NotBeNull();
        clientProvider2.Should().NotBeNull();
    }

    [Test]
    public async Task AddMessageQueues_WhenOneConnectionWithOneProducerAndOneConsumer_ThenShouldRegisterBoth()
    {
        var appSettings = MqHelpers.CreateAppSettings(
            [MqHelpers.CreateConnection(_connection1Name, "rabbit", container: GlobalFixture.RabbitMqInstance1)],
            [MqHelpers.CreateConsumer(_consumer1Name, _connection1Name)]);

        await using var sut = await InitHost(appSettings, builder =>
        {
            builder.AddRabbitMqClientProviders()
                .AddRabbitMqProducers()
                .AddProducer<TestMessage1>(_connection1Name, _producer1Name)
                .AddRabbitMqConsumers()
                .AddConsumer<TestMessage1, TestConsumer1>(_consumer1Name)
                .Build();
        });

        var scopeFactory = sut.Services.GetRequiredService<IServiceScopeFactory>();
        using var serviceScope = scopeFactory.CreateScope();

        var producer = serviceScope.ServiceProvider.GetKeyedService<IProducer<TestMessage1>>(_producer1Name);
        var consumerInitializer = serviceScope.ServiceProvider.GetKeyedService<IConsumerInitializer>(_consumer1Name);
        var consumer = serviceScope.ServiceProvider.GetService<IConsumer<TestMessage1>>();

        producer.Should().NotBeNull();
        consumerInitializer.Should().NotBeNull();
        consumer.Should().NotBeNull();
    }

    [Test]
    public async Task AddMessageQueues_WhenTwoConnectionsWithTwoProducersAndTwoConsumers_ThenShouldRegisterAll()
    {
        var appSettings = MqHelpers.CreateAppSettings(
            [
                MqHelpers.CreateConnection(_connection1Name, "rabbit", container : GlobalFixture.RabbitMqInstance1),
                MqHelpers.CreateConnection(_connection2Name, "rabbit", container: GlobalFixture.RabbitMqInstance2)
            ],
            [
                MqHelpers.CreateConsumer(_consumer1Name, _connection1Name),
                MqHelpers.CreateConsumer(_consumer2Name, _connection2Name)
            ]);

        await using var sut = await InitHost(appSettings, builder =>
        {
            builder.AddRabbitMqClientProviders()
                .AddRabbitMqProducers()
                .AddProducer<TestMessage1>(_connection1Name, _producer1Name)
                .AddProducer<TestMessage2>(_connection2Name, _producer2Name)
                .AddRabbitMqConsumers()
                .AddConsumer<TestMessage1, TestConsumer1>(_consumer1Name)
                .AddConsumer<TestMessage2, TestConsumer2>(_consumer2Name)
                .Build();
        });

        var producer1 = sut.Services.GetKeyedService<IProducer<TestMessage1>>(_producer1Name);
        var producer2 = sut.Services.GetKeyedService<IProducer<TestMessage2>>(_producer2Name);
        var consumerInitializer1 = sut.Services.GetKeyedService<IConsumerInitializer>(_consumer1Name);
        var consumerInitializer2 = sut.Services.GetKeyedService<IConsumerInitializer>(_consumer2Name);

        producer1.Should().NotBeNull();
        producer2.Should().NotBeNull();
        consumerInitializer1.Should().NotBeNull();
        consumerInitializer2.Should().NotBeNull();
    }

    [Test]
    public async Task AddMessageQueues_WhenOneConnectionWithMultipleProducersAndConsumers_ThenShouldRegisterAll()
    {
        var appSettings = MqHelpers.CreateAppSettings(
            [MqHelpers.CreateConnection(_connection1Name, "rabbit", container: GlobalFixture.RabbitMqInstance1)],
            [
                MqHelpers.CreateConsumer(_consumer1Name, _connection1Name),
                MqHelpers.CreateConsumer(_consumer2Name, _connection1Name)
            ]);

        await using var sut = await InitHost(appSettings, builder =>
        {
            builder.AddRabbitMqClientProviders()
                .AddRabbitMqProducers()
                .AddProducer<TestMessage1>(_connection1Name, _producer1Name)
                .AddProducer<TestMessage2>(_connection1Name, _producer2Name)
                .AddRabbitMqConsumers()
                .AddConsumer<TestMessage1, TestConsumer1>(_consumer1Name)
                .AddConsumer<TestMessage2, TestConsumer2>(_consumer2Name)
                .Build();
        });

        var producer1 = sut.Services.GetKeyedService<IProducer<TestMessage1>>(_producer1Name);
        var producer2 = sut.Services.GetKeyedService<IProducer<TestMessage2>>(_producer2Name);
        var consumerInitializer1 = sut.Services.GetKeyedService<IConsumerInitializer>(_consumer1Name);
        var consumerInitializer2 = sut.Services.GetKeyedService<IConsumerInitializer>(_consumer2Name);

        producer1.Should().NotBeNull();
        producer2.Should().NotBeNull();
        consumerInitializer1.Should().NotBeNull();
        consumerInitializer2.Should().NotBeNull();
    }

    [Test]
    public async Task AddMessageQueues_WhenProducerRegisteredWithoutRabbitMqProducers_ThenShouldNotRegisterProducer()
    {
        var appSettings = MqHelpers.CreateAppSettings(
            [MqHelpers.CreateConnection(_connection1Name, "rabbit", container: GlobalFixture.RabbitMqInstance1)],
            []);

        await using var sut = await InitHost(appSettings, builder =>
        {
            builder.AddRabbitMqClientProviders()
                .AddProducer<TestMessage1>(_connection1Name, _producer1Name)
                .Build();
        });

        var producer = sut.Services.GetKeyedService<IProducer<TestMessage1>>(_producer1Name);

        producer.Should().BeNull();
    }

    [Test]
    public async Task AddMessageQueues_WhenConsumerRegisteredWithoutRabbitMqConsumers_ThenShouldNotRegisterConsumerHostedService()
    {
        var appSettings = MqHelpers.CreateAppSettings(
            [MqHelpers.CreateConnection(_connection1Name, "rabbit", container: GlobalFixture.RabbitMqInstance1)],
            [MqHelpers.CreateConsumer(_consumer1Name, _connection1Name)]);

        await using var sut = await InitHost(appSettings, builder =>
        {
            builder.AddRabbitMqClientProviders()
                .AddConsumer<TestMessage1, TestConsumer1>(_consumer1Name)
                .Build();
        });

        var consumerInitializer = sut.Services.GetKeyedService<IConsumerInitializer>(_consumer1Name);

        consumerInitializer.Should().BeNull();
    }

    [Test]
    public async Task AddMessageQueues_WhenConsumerWithParallelism_ThenEachWorkerShouldShareConnection()
    {
        var parallelism = 4;
        var appSettings = MqHelpers.CreateAppSettings(
            [
                MqHelpers.CreateConnection(_connection1Name, "rabbit",
                parallelism: parallelism,
                container: GlobalFixture.RabbitMqInstance1)
            ],
            [
                MqHelpers.CreateConsumer(_consumer1Name, _connection1Name, parallelism: parallelism)
            ]);

        await using var sut = await InitHost(appSettings, builder =>
        {
            builder.AddRabbitMqClientProviders()
                .AddRabbitMqConsumers()
                .AddConsumer<TestMessage1, TestConsumer1>(_consumer1Name)
                .Build();
        });

        var clients = new List<IConnection>();

        foreach (var expectedProviderKey in Enumerable.Range(0, parallelism).Select(i => _consumer1Name + i)
                     .Concat([_consumer1Name]))
        {
            var client = sut.Services.GetKeyedService<IClientProvider<IConnection>>(expectedProviderKey);

            client.Should().NotBeNull($"Client provider for {expectedProviderKey} should be registered");
            using var lease = client.Acquire();
            lease.IsInitialized.Should().BeTrue($"Client provider for {expectedProviderKey} should be initialized");

            clients.Add(lease.Client);
        }

        clients.Distinct().Should().HaveCount(1, "Client providers should be registered one per consumer");
    }

    [Test]
    public async Task AddMessageQueues_WhenTwoConsumersWithParallelism_ThenConnectionPerConsumerShouldBeRegistered()
    {
        var parallelism = 4;
        var appSettings = MqHelpers.CreateAppSettings(
            [
                MqHelpers.CreateConnection(_connection1Name,
                    "rabbit",
                    parallelism: parallelism,
                    container: GlobalFixture.RabbitMqInstance1)
            ],
            [
                MqHelpers.CreateConsumer(_consumer1Name, _connection1Name, parallelism: parallelism),
                MqHelpers.CreateConsumer(_consumer2Name, _connection1Name, parallelism: parallelism)
            ]);

        await using var sut = await InitHost(appSettings, builder =>
        {
            builder.AddRabbitMqClientProviders()
                .AddRabbitMqConsumers()
                .AddConsumer<TestMessage1, TestConsumer1>(_consumer1Name)
                .AddConsumer<TestMessage2, TestConsumer2>(_consumer2Name)
                .Build();
        });

        var clients = new List<IConnection>();

        foreach (var expectedProviderKey in Enumerable.Range(0, parallelism).SelectMany(i => new[] { _consumer1Name + i, _consumer2Name + i })
                     .Concat([_consumer1Name, _consumer2Name]))
        {
            var client = sut.Services.GetKeyedService<IClientProvider<IConnection>>(expectedProviderKey);

            client.Should().NotBeNull($"Client provider for {expectedProviderKey} should be registered");
            using var lease = client.Acquire();
            lease.IsInitialized.Should().BeTrue($"Client provider for {expectedProviderKey} should be initialized");

            clients.Add(lease.Client);
        }

        clients.Distinct().Should().HaveCount(2, "Client providers should be registered one per consumer");
    }

    #region Test Message Types and Consumers

    public class TestMessage1
    {
        public Guid Id { get; init; }
        public string Content { get; init; } = string.Empty;
    }

    public class TestMessage2
    {
        public Guid Id { get; init; }
        public string Data { get; init; } = string.Empty;
    }

    public class TestConsumer1 : IConsumer<TestMessage1>
    {
        public Task<bool> HandleMessageAsync(MessageEnvelop<TestMessage1> message, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }

    public class TestConsumer2 : IConsumer<TestMessage2>
    {
        public Task<bool> HandleMessageAsync(MessageEnvelop<TestMessage2> message, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }

    #endregion
}