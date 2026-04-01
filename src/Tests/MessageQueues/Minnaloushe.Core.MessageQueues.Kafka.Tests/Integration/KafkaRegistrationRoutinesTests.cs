using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Minnaloushe.Core.ClientProviders.Kafka;
using Minnaloushe.Core.ClientProviders.Kafka.Wrappers;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Extensions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Kafka.Extensions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Models;
using Minnaloushe.Core.MessageQueues.Kafka.Consumers.Extensions;
using Minnaloushe.Core.MessageQueues.Kafka.Producers;
using Minnaloushe.Core.MessageQueues.Kafka.Producers.Extensions;
using Minnaloushe.Core.Tests.Helpers;
using Minnaloushe.Core.Toolbox.AsyncInitializer.Extensions;
using Minnaloushe.Core.Toolbox.JsonConfiguration;
using Minnaloushe.Core.Toolbox.TestHelpers;

namespace Minnaloushe.Core.MessageQueues.Kafka.Tests.Integration;

[TestFixture]
[Category("Unit")]
public class KafkaRegistrationRoutinesTests
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
            [MqHelpers.CreateConnection(
                _connection1Name,
                type: "kafka", connectionString: GlobalFixture.Kafka1.Instance.GetBootstrapAddress())],
            []);

        await using var sut = await InitHost(appSettings, builder =>
        {
            builder.AddKafkaClientProviders().Build();
        });

        var consumerProvider = sut.Services.GetKeyedService<IKafkaConsumerClientProvider>(_connection1Name);
        var adminProvider = sut.Services.GetKeyedService<IKafkaAdminClientProvider>(_connection1Name);
        var producerProvider = sut.Services.GetKeyedService<IKafkaProducerClientProvider>(_connection1Name);

        consumerProvider.Should().NotBeNull();
        adminProvider.Should().NotBeNull();
        producerProvider.Should().NotBeNull();
    }

    [Test]
    public async Task AddMessageQueues_WhenTwoConnectionsWithoutConsumersOrProducers_ThenShouldRegisterBothClientProviders()
    {
        var appSettings = MqHelpers.CreateAppSettings(
            [
                MqHelpers.CreateConnection(_connection1Name,
                    type: "kafka", connectionString: GlobalFixture.Kafka1.Instance.GetBootstrapAddress()),
                MqHelpers.CreateConnection(
                    _connection2Name,
                    type: "kafka", connectionString: GlobalFixture.Kafka2.Instance.GetBootstrapAddress())
            ],
            []);

        await using var sut = await InitHost(appSettings, builder =>
        {
            builder.AddKafkaClientProviders().Build();
        });

        var consumerProvider1 = sut.Services.GetKeyedService<IKafkaConsumerClientProvider>(_connection1Name);
        var consumerProvider2 = sut.Services.GetKeyedService<IKafkaConsumerClientProvider>(_connection2Name);
        var adminProvider1 = sut.Services.GetKeyedService<IKafkaAdminClientProvider>(_connection1Name);
        var adminProvider2 = sut.Services.GetKeyedService<IKafkaAdminClientProvider>(_connection2Name);
        var producerProvider1 = sut.Services.GetKeyedService<IKafkaProducerClientProvider>(_connection1Name);
        var producerProvider2 = sut.Services.GetKeyedService<IKafkaProducerClientProvider>(_connection2Name);

        consumerProvider1.Should().NotBeNull();
        consumerProvider2.Should().NotBeNull();
        consumerProvider1.Should().NotBeSameAs(consumerProvider2);

        adminProvider1.Should().NotBeNull();
        adminProvider2.Should().NotBeNull();
        adminProvider1.Should().NotBeSameAs(adminProvider2);

        producerProvider1.Should().NotBeNull();
        producerProvider2.Should().NotBeNull();
        producerProvider1.Should().NotBeSameAs(producerProvider2);
    }

    [Test]
    public async Task AddMessageQueues_WhenOneConnectionWithOneProducer_ThenShouldRegisterProducer()
    {
        var appSettings = MqHelpers.CreateAppSettings(
            [MqHelpers.CreateConnection(
                _connection1Name,
                type: "kafka", connectionString: GlobalFixture.Kafka1.Instance.GetBootstrapAddress())],
            []);

        await using var sut = await InitHost(appSettings, builder =>
        {
            builder.AddKafkaClientProviders()
                .AddKafkaProducers()
                .AddProducer<TestMessage1>(_connection1Name, _producer1Name)
                .Build();
        });

        var producer = sut.Services.GetKeyedService<IProducer<TestMessage1>>(_producer1Name);
        var concreteProducer = sut.Services.GetKeyedService<KafkaProducer<TestMessage1>>(_producer1Name);

        producer.Should().NotBeNull();
        concreteProducer.Should().NotBeNull();
        producer.Should().BeSameAs(concreteProducer);
    }

    [Test]
    public async Task AddMessageQueues_WhenProducerIsRegistered_ThenShouldRegisterProducerAndAdminProvidersPerProducerKey()
    {
        var appSettings = MqHelpers.CreateAppSettings(
            [MqHelpers.CreateConnection(
                _connection1Name,
                type: "kafka", connectionString: GlobalFixture.Kafka1.Instance.GetBootstrapAddress())],
            []);

        await using var sut = await InitHost(appSettings, builder =>
        {
            builder.AddKafkaClientProviders()
                .AddKafkaProducers()
                .AddProducer<TestMessage1>(_connection1Name, _producer1Name)
                .Build();
        });

        var producerClientProvider = sut.Services.GetKeyedService<IKafkaProducerClientProvider>(_producer1Name);
        var adminClientProvider = sut.Services.GetKeyedService<IKafkaAdminClientProvider>(_connection1Name);
        var connectionProducerProvider = sut.Services.GetKeyedService<IKafkaProducerClientProvider>(_connection1Name);

        producerClientProvider.Should().NotBeNull();
        adminClientProvider.Should().NotBeNull();
        connectionProducerProvider.Should().NotBeNull();
        producerClientProvider.Should().BeSameAs(connectionProducerProvider);
    }

    [Test]
    public async Task AddMessageQueues_WhenOneConnectionWithTwoProducers_ThenShouldRegisterBothProducers()
    {
        var appSettings = MqHelpers.CreateAppSettings(
            [MqHelpers.CreateConnection(
                _connection1Name,
                type: "kafka", connectionString: GlobalFixture.Kafka1.Instance.GetBootstrapAddress())],
            []);

        await using var sut = await InitHost(appSettings, builder =>
        {
            builder.AddKafkaClientProviders()
                .AddKafkaProducers()
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
                MqHelpers.CreateConnection(
                    _connection1Name,
                    type: "kafka", connectionString: GlobalFixture.Kafka1.Instance.GetBootstrapAddress()),
                MqHelpers.CreateConnection(
                    _connection2Name,
                    type: "kafka", connectionString: GlobalFixture.Kafka2.Instance.GetBootstrapAddress())
            ],
            []);

        await using var sut = await InitHost(appSettings, builder =>
        {
            builder.AddKafkaClientProviders()
                .AddKafkaProducers()
                .AddProducer<TestMessage1>(_connection1Name, _producer1Name)
                .AddProducer<TestMessage2>(_connection2Name, _producer2Name)
                .Build();
        });

        var producer1 = sut.Services.GetKeyedService<IProducer<TestMessage1>>(_producer1Name);
        var producer2 = sut.Services.GetKeyedService<IProducer<TestMessage2>>(_producer2Name);
        var clientProvider1 = sut.Services.GetKeyedService<IKafkaProducerClientProvider>(_producer1Name);
        var clientProvider2 = sut.Services.GetKeyedService<IKafkaProducerClientProvider>(_producer2Name);

        producer1.Should().NotBeNull();
        producer2.Should().NotBeNull();
        clientProvider1.Should().NotBeNull();
        clientProvider2.Should().NotBeNull();
    }

    [Test]
    public async Task AddMessageQueues_WhenOneConnectionWithOneConsumer_ThenShouldRegisterConsumer()
    {
        var appSettings = MqHelpers.CreateAppSettings(
            [MqHelpers.CreateConnection(
                _connection1Name,
                type: "kafka", connectionString: GlobalFixture.Kafka1.Instance.GetBootstrapAddress())],
            [MqHelpers.CreateConsumer(_consumer1Name, _connection1Name)]);

        await using var sut = await InitHost(appSettings, builder =>
        {
            builder.AddKafkaClientProviders()
                .AddKafkaConsumers()
                .AddConsumer<TestMessage1, TestConsumer1>(_consumer1Name)
                .Build();
        });

        var scopeFactory = sut.Services.GetRequiredService<IServiceScopeFactory>();
        using var serviceScope = scopeFactory.CreateScope();

        var consumerInitializer = serviceScope.ServiceProvider.GetKeyedService<IConsumerInitializer>(_consumer1Name);
        var messageEngine = serviceScope.ServiceProvider.GetService<IMessageEngineFactory<TestMessage1?, IKafkaConsumerClientWrapper>>();
        var consumer = serviceScope.ServiceProvider.GetService<IConsumer<TestMessage1>>();

        consumerInitializer.Should().NotBeNull();
        messageEngine.Should().NotBeNull();
        consumer.Should().NotBeNull();
    }

    [Test]
    public async Task AddMessageQueues_WhenOneConnectionWithTwoConsumers_ThenShouldRegisterBothConsumers()
    {
        var appSettings = MqHelpers.CreateAppSettings(
            [MqHelpers.CreateConnection(
                _connection1Name,
                type: "kafka", connectionString: GlobalFixture.Kafka1.Instance.GetBootstrapAddress())],
            [
                MqHelpers.CreateConsumer(_consumer1Name, _connection1Name),
                MqHelpers.CreateConsumer(_consumer2Name, _connection1Name)
            ]);

        await using var sut = await InitHost(appSettings, builder =>
        {
            builder.AddKafkaClientProviders()
                .AddKafkaConsumers()
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
                MqHelpers.CreateConnection(
                    _connection1Name,
                    type: "kafka", connectionString: GlobalFixture.Kafka1.Instance.GetBootstrapAddress(), username: GlobalFixture.Kafka1.Username, password: GlobalFixture.Kafka1.Password),
                MqHelpers.CreateConnection(
                    _connection2Name,
                    type: "kafka", connectionString: GlobalFixture.Kafka2.Instance.GetBootstrapAddress(), username: GlobalFixture.Kafka1.Username, password: GlobalFixture.Kafka2.Password)
            ],
            [
                MqHelpers.CreateConsumer(_consumer1Name, _connection1Name),
                MqHelpers.CreateConsumer(_consumer2Name, _connection2Name)
            ]);

        await using var sut = await InitHost(appSettings, builder =>
        {
            builder.AddKafkaClientProviders()
                .AddKafkaConsumers()
                .AddConsumer<TestMessage1, TestConsumer1>(_consumer1Name)
                .AddConsumer<TestMessage2, TestConsumer2>(_consumer2Name)
                .Build();
        });

        var consumerInitializer1 = sut.Services.GetKeyedService<IConsumerInitializer>(_consumer1Name);
        var consumerInitializer2 = sut.Services.GetKeyedService<IConsumerInitializer>(_consumer2Name);
        var clientProvider1 = sut.Services.GetKeyedService<IKafkaConsumerClientProvider>(_consumer1Name);
        var clientProvider2 = sut.Services.GetKeyedService<IKafkaConsumerClientProvider>(_consumer2Name);

        consumerInitializer1.Should().NotBeNull();
        consumerInitializer2.Should().NotBeNull();
        clientProvider1.Should().NotBeNull();
        clientProvider2.Should().NotBeNull();
    }

    [Test]
    public async Task AddMessageQueues_WhenOneConnectionWithOneProducerAndOneConsumer_ThenShouldRegisterBoth()
    {
        var appSettings = MqHelpers.CreateAppSettings(
            [MqHelpers.CreateConnection(
                _connection1Name,
                type: "kafka", connectionString: GlobalFixture.Kafka1.Instance.GetBootstrapAddress())],
            [MqHelpers.CreateConsumer(_consumer1Name, _connection1Name)]);

        await using var sut = await InitHost(appSettings, builder =>
        {
            builder.AddKafkaClientProviders()
                .AddKafkaProducers()
                .AddProducer<TestMessage1>(_connection1Name, _producer1Name)
                .AddKafkaConsumers()
                .AddConsumer<TestMessage1, TestConsumer1>(_consumer1Name)
                .Build();
        });
        var scopeFactory = sut.Services.GetRequiredService<IServiceScopeFactory>();
        using var serviceScope = scopeFactory.CreateScope();

        var producer = sut.Services.GetKeyedService<IProducer<TestMessage1>>(_producer1Name);
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
                MqHelpers.CreateConnection(_connection1Name,
                    type: "kafka", connectionString: GlobalFixture.Kafka1.Instance.GetBootstrapAddress()),
                MqHelpers.CreateConnection(_connection2Name,
                    "kafka", GlobalFixture.Kafka2.Instance.GetBootstrapAddress())
            ],
            [
                MqHelpers.CreateConsumer(_consumer1Name, _connection1Name),
                MqHelpers.CreateConsumer(_consumer2Name, _connection2Name)
            ]);

        await using var sut = await InitHost(appSettings, builder =>
        {
            builder.AddKafkaClientProviders()
                .AddKafkaProducers()
                .AddProducer<TestMessage1>(_connection1Name, _producer1Name)
                .AddProducer<TestMessage2>(_connection2Name, _producer2Name)
                .AddKafkaConsumers()
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
            [MqHelpers.CreateConnection(
                _connection1Name,
                type: "kafka", connectionString: GlobalFixture.Kafka1.Instance.GetBootstrapAddress())],
            [
                MqHelpers.CreateConsumer(_consumer1Name, _connection1Name),
                MqHelpers.CreateConsumer(_consumer2Name, _connection1Name)
            ]);

        await using var sut = await InitHost(appSettings, builder =>
        {
            builder.AddKafkaClientProviders()
                .AddKafkaProducers()
                .AddProducer<TestMessage1>(_connection1Name, _producer1Name)
                .AddProducer<TestMessage2>(_connection1Name, _producer2Name)
                .AddKafkaConsumers()
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
    public async Task AddMessageQueues_WhenProducerRegisteredWithoutKafkaProducers_ThenShouldNotRegisterProducer()
    {
        var appSettings = MqHelpers.CreateAppSettings(
            [MqHelpers.CreateConnection(
                _connection1Name,
                type: "kafka", connectionString: GlobalFixture.Kafka1.Instance.GetBootstrapAddress())],
            []);

        await using var sut = await InitHost(appSettings, builder =>
        {
            builder.AddKafkaClientProviders()
                .AddProducer<TestMessage1>(_connection1Name, _producer1Name)
                .Build();
        });

        var producer = sut.Services.GetKeyedService<IProducer<TestMessage1>>(_producer1Name);

        producer.Should().BeNull();
    }

    [Test]
    public async Task AddMessageQueues_WhenConsumerRegisteredWithoutKafkaConsumers_ThenShouldNotRegisterConsumerHostedService()
    {
        var appSettings = MqHelpers.CreateAppSettings(
            [MqHelpers.CreateConnection(
                _connection1Name,
                type: "kafka", connectionString: GlobalFixture.Kafka1.Instance.GetBootstrapAddress())],
            [MqHelpers.CreateConsumer(_consumer1Name, _connection1Name)]);

        await using var sut = await InitHost(appSettings, builder =>
        {
            builder.AddKafkaClientProviders()
                .AddConsumer<TestMessage1, TestConsumer1>(_consumer1Name)
                .Build();
        });

        var consumerInitializer = sut.Services.GetKeyedService<IConsumerInitializer>(_consumer1Name);

        consumerInitializer.Should().BeNull();
    }

    [Test]
    public async Task AddMessageQueues_WhenConsumerWithParallelism_ThenEachWorkerShouldGetDedicatedConnection()
    {
        var parallelism = 4;
        var appSettings = MqHelpers.CreateAppSettings(
            [MqHelpers.CreateConnection(
                _connection1Name,
                type: "kafka", connectionString: GlobalFixture.Kafka1.Instance.GetBootstrapAddress())],
            [MqHelpers.CreateConsumer(_consumer1Name, _connection1Name, parallelism: parallelism)]);

        await using var sut = await InitHost(appSettings, builder =>
        {
            builder.AddKafkaClientProviders()
                .AddKafkaConsumers()
                .AddConsumer<TestMessage1, TestConsumer1>(_consumer1Name)
                .Build();
        });

        var clients = new List<IKafkaConsumerClientWrapper>();

        foreach (var expectedProviderKey in Enumerable.Range(0, parallelism).Select(i => _consumer1Name + i)
                     .Concat([_consumer1Name]))
        {
            var client = sut.Services.GetKeyedService<IKafkaConsumerClientProvider>(expectedProviderKey);

            client.Should().NotBeNull($"Client provider for {expectedProviderKey} should be registered");
            using var lease = client.Acquire();
            lease.IsInitialized.Should().BeTrue($"Client provider for {expectedProviderKey} should be initialized");

            clients.Add(lease.Client);
        }

        clients.Should().OnlyHaveUniqueItems("All keyed client providers should be created as dedicated instances");
    }

    [Test]
    public async Task AddMessageQueues_WhenTwoConsumersWithParallelism_ThenEachShouldHaveDedicatedConnection()
    {
        var parallelism = 4;
        var appSettings = MqHelpers.CreateAppSettings(
            [MqHelpers.CreateConnection(
                _connection1Name,
                type: "kafka",
                connectionString: GlobalFixture.Kafka1.Instance.GetBootstrapAddress(), parallelism: parallelism)],
            [
                MqHelpers.CreateConsumer(_consumer1Name, _connection1Name, parallelism: parallelism),
                MqHelpers.CreateConsumer(_consumer2Name, _connection1Name, parallelism: parallelism)
            ]);

        await using var sut = await InitHost(appSettings, builder =>
        {
            builder.AddKafkaClientProviders()
                .AddKafkaConsumers()
                .AddConsumer<TestMessage1, TestConsumer1>(_consumer1Name)
                .AddConsumer<TestMessage2, TestConsumer2>(_consumer2Name)
                .Build();
        });

        var clients = new List<IKafkaConsumerClientWrapper>();

        foreach (var expectedProviderKey in Enumerable.Range(0, parallelism).SelectMany(i => new[] { _consumer1Name + i, _consumer2Name + i })
                     .Concat([_consumer1Name, _consumer2Name]))
        {
            var client = sut.Services.GetKeyedService<IKafkaConsumerClientProvider>(expectedProviderKey);

            client.Should().NotBeNull($"Client provider for {expectedProviderKey} should be registered");
            using var lease = client.Acquire();
            lease.IsInitialized.Should().BeTrue($"Client provider for {expectedProviderKey} should be initialized");

            clients.Add(lease.Client);
        }

        clients.Should().OnlyHaveUniqueItems("All keyed client providers should be created as dedicated instances");
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