using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Minnaloushe.Core.ClientProviders.Abstractions;
using Minnaloushe.Core.MessageQueue.RabbitMq.Tests.TestClasses;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Extensions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.RabbitMq.Extensions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.RabbitMq.Vault.Extensions;
using Minnaloushe.Core.MessageQueues.RabbitMq.Producers;
using Minnaloushe.Core.ServiceDiscovery.Abstractions;
using Minnaloushe.Core.ServiceDiscovery.Entities;
using Minnaloushe.Core.ServiceDiscovery.Extensions;
using Minnaloushe.Core.ServiceDiscovery.Routines;
using Minnaloushe.Core.Tests.Helpers;
using Minnaloushe.Core.Toolbox.AsyncInitializer.Extensions;
using Minnaloushe.Core.Toolbox.TestHelpers;
using Minnaloushe.Core.VaultService.Extensions;
using Moq;
using RabbitMQ.Client;

namespace Minnaloushe.Core.MessageQueue.RabbitMq.Tests.Integration;

[TestFixture]
[Category("TestContainers")]
[Category("Integration")]
public class RabbitMqVaultIntegrationTests
{
    private TestHost _testHost = null!;

    private readonly string _connectionName = Helpers.UniqueString("rabbit-connection");
    private readonly string _consumerName = Helpers.UniqueString("test-consumer");
    private readonly string _serviceKey = Helpers.UniqueString("test-queue");

    private readonly Mock<IInfrastructureConventionProvider> _mockAppDependencies = new();
    private readonly Mock<IServiceDiscoveryService> _mockServiceDiscovery = new();

    public static readonly AsyncThresholdCollection<TestMessage> ReceivedMessages = new();

    private const int MessageDelayMs = 2000;
    private const int WaitTimeoutSeconds = 5;

    private object AppSettings
    {
        get
        {
            var vaultAddress = new UriBuilder(
                Uri.UriSchemeHttp,
                GlobalFixture.Vault.Instance.Hostname,
                GlobalFixture.Vault.Instance.GetMappedPublicPort(8200)
            ).Uri.ToString().TrimEnd('/');

            return new
            {
                Vault = new
                {
                    Address = vaultAddress,
                    Token = GlobalFixture.Vault.Password,
                    Scheme = "http",
                    ServiceName = GlobalFixture.Vault.Name,
                    MountPoint = "kv"
                },
                MessageQueues = new
                {
                    Connections = new[]
                    {
                        MqHelpers.CreateConnection(_connectionName, type: "rabbit-static",
                            username: string.Empty, password: string.Empty,
                            serviceName: GlobalFixture.RabbitMqInstance1.Name,
                            serviceKey: "test-rabbit-vault")
                    },
                    Consumers = new[]
                    {
                        MqHelpers.CreateConsumer(_consumerName, _connectionName)
                    }
                },
                AsyncInitializer = new
                {
                    Enabled = true,
                    Timeout = TimeSpan.FromMinutes(2)
                }
            };
        }
    }

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        ReceivedMessages.Clear();

        _mockAppDependencies.Setup(x => x.GetStaticSecretPath(It.IsAny<string>()))
            .ReturnsAsync((string serviceName) => $"{GlobalFixture.AppNamespace}/{serviceName}");
        _mockAppDependencies.Setup(x => x.GetApplicationNamespace())
            .ReturnsAsync(GlobalFixture.AppNamespace);

        _mockServiceDiscovery.Setup(x => x.ResolveServiceEndpoint(
                GlobalFixture.RabbitMqInstance1.Name,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ServiceEndpoint
            {
                Host = GlobalFixture.RabbitMqInstance1.Host,
                Port = GlobalFixture.RabbitMqInstance1.Port
            }]);

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

                services.AddSingleton(configuration);

                services.AddServiceDiscovery();
                services.AddSingleton(_mockAppDependencies.Object);
                //services.AddSingleton(_mockServiceDiscovery.Object);

                services.ConfigureAsyncInitializers();

                services.AddVaultClientProvider();

                services.AddMessageQueues(configuration)
                    .AddRabbitMqClientProviders()
                    .AddVaultRabbitMqClientProviders()
                    .AddRabbitMqConsumers()
                    .AddConsumer<TestMessage, TestVaultIntegrationConsumer>(_consumerName)
                    .AddRabbitMqProducers()
                    .AddProducer<TestMessage>(_connectionName)
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
    public void SetUp()
    {
        ReceivedMessages.Clear();
    }

    [Test]
    public async Task PublishAsync_WhenMessagePublished_ThenShouldBeConsumed()
    {
        var producer = _testHost.Services.GetRequiredService<IProducer<TestMessage>>();
        var testMessage = new TestMessage { Data = $"VaultTest-{Guid.NewGuid()}" };

        await producer.PublishAsync(testMessage, cancellationToken: CancellationToken.None);

        await ReceivedMessages.WaitUntilCountAtLeastAsync(5, TimeSpan.FromSeconds(WaitTimeoutSeconds));

        var snapshot = ReceivedMessages.GetSnapshot();
        snapshot.Should().NotBeEmpty();
        snapshot.Should().Contain(m => m.Data == testMessage.Data);
    }

    [Test]
    public async Task PublishAsync_WhenPublishMultipleMessages_ThenShouldConsumeAll()
    {
        var producer = _testHost.Services.GetRequiredService<IProducer<TestMessage>>();
        var messages = Enumerable.Range(1, 5)
            .Select(i => new TestMessage { Data = $"VaultMessage-{i}-{Guid.NewGuid()}" })
            .ToList();

        foreach (var message in messages)
        {
            await producer.PublishAsync(message, cancellationToken: CancellationToken.None);
        }

        await ReceivedMessages.WaitUntilCountAtLeastAsync(5, TimeSpan.FromSeconds(WaitTimeoutSeconds));
        var snapshot = ReceivedMessages.GetSnapshot();

        snapshot.Count.Should().BeGreaterThanOrEqualTo(messages.Count);

        foreach (var message in messages)
        {
            snapshot.Should().Contain(m => m.Data == message.Data);
        }
    }

    [Test]
    public async Task PublishAsync_WhenPublishWithHeaders_ThenShouldWork()
    {
        var producer = _testHost.Services.GetRequiredService<IProducer<TestMessage>>();
        var testMessage = new TestMessage { Data = $"VaultHeaderTest-{Guid.NewGuid()}" };
        var headers = new Dictionary<string, string>
        {
            ["custom-header"] = "custom-value",
            ["correlation-id"] = Guid.NewGuid().ToString()
        };

        await producer.PublishAsync(testMessage, null, headers, CancellationToken.None);

        await ReceivedMessages.WaitUntilCountAtLeastAsync(1, TimeSpan.FromSeconds(WaitTimeoutSeconds));

        ReceivedMessages.GetSnapshot().Should().Contain(m => m.Data == testMessage.Data);
    }

    [Test]
    public void ProducerAndConsumer_WhenRegistered_ThenShouldBeResolvable()
    {
        var scopeFactory = _testHost.Services.GetRequiredService<IServiceScopeFactory>();
        using var serviceScope = scopeFactory.CreateScope();

        var producer = _testHost.Services.GetService<IProducer<TestMessage>>();
        var consumer = serviceScope.ServiceProvider.GetService<IConsumer<TestMessage>>();

        producer.Should().NotBeNull();
        consumer.Should().NotBeNull();
    }

    [Test]
    public void ClientProvider_WhenUsingVaultCredentials_ThenShouldBeResolvable()
    {
        var clientProvider = _testHost.Services.GetKeyedService<IClientProvider<IConnection>>(_connectionName);
        clientProvider.Should().NotBeNull();
    }
}

public class TestVaultIntegrationConsumer(ILogger<TestVaultIntegrationConsumer> logger) : IConsumer<TestMessage>
{
    public Task<bool> HandleMessageAsync(MessageEnvelop<TestMessage> envelop, CancellationToken cancellationToken = default)
    {
#pragma warning disable CA1873
        logger.LogInformation("Received message from Vault-configured RabbitMQ: {Data}", envelop.Message.Data);
#pragma warning restore CA1873
        RabbitMqVaultIntegrationTests.ReceivedMessages.Add(envelop.Message);
        return Task.FromResult(true);
    }
}