using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Minnaloushe.Core.ClientProviders.Abstractions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Extensions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.RabbitMq.Vault.Extensions;
using Minnaloushe.Core.ServiceDiscovery.Abstractions;
using Minnaloushe.Core.ServiceDiscovery.Entities;
using Minnaloushe.Core.ServiceDiscovery.Routines;
using Minnaloushe.Core.Toolbox.AsyncInitializer.Extensions;
using Minnaloushe.Core.Toolbox.TestHelpers;
using Minnaloushe.Core.VaultService.Extensions;
using RabbitMQ.Client;

namespace Minnaloushe.Core.ClientProviders.RabbitMq.Tests.Integration;

/// <summary>
/// Integration tests for RabbitMQ client provider DI registration (Vault/Static version).
/// Verifies that AddVaultRabbitMqClientProviders correctly registers:
/// - IClientProvider<IConnection> instances keyed by connection name
/// - Each connection has ServiceName configured (credentials come from Vault)
/// - Providers are singletons
/// - Providers can acquire connections successfully
/// 
/// Note: Connections are only registered when they have at least one consumer configured.
/// Note: This tests the Vault version where credentials are fetched from Vault.
/// Note: Uses TestContainers to spin up Vault and RabbitMQ instances.
/// </summary>
[TestFixture]
[Category("Integration")]
[Category("TestContainers")]
public class RabbitMqClientProviderDIRegistrationTests
{
    #region Constants

    private const string Connection1Name = "rabbit-conn1";
    private const string Connection2Name = "rabbit-conn2";

    #endregion

    #region Fields/Members

    private TestHost _sut = null!;

    private readonly Mock<IServiceDiscoveryService> _mockServiceDiscovery = new();
    private readonly Mock<IInfrastructureConventionProvider> _mockDependenciesProvider = new();

    #endregion

    #region Setup/Teardown

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        SetupMocks();

        _sut = await TestHost.Build(
            configureConfiguration: cfg => cfg.AddConfiguration(AppSettings),
            configureServices: (services, configuration) =>
            {
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Debug);
                });

                services.AddSingleton(configuration);
                services.AddSingleton(_mockServiceDiscovery.Object);
                services.AddSingleton(_mockDependenciesProvider.Object);

                services.ConfigureAsyncInitializers();

                services.AddVaultClientProvider();

                services.AddMessageQueues(configuration)
                    .AddVaultRabbitMqClientProviders()
                    .Build();
            },
            beforeStart: async host =>
            {
                await host.InvokeAsyncInitializers();
            },
            startHost: false);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _sut.DisposeAsync();
    }

    #endregion

    #region Helper Methods

    private void SetupMocks()
    {
        _mockServiceDiscovery
            .Setup(x => x.ResolveServiceEndpoint(
                GlobalFixture.RabbitMqInstance1.Name,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ServiceEndpoint
            {
                Host = GlobalFixture.RabbitMqInstance1.Host,
                Port = GlobalFixture.RabbitMqInstance1.Port
            }]);

        _mockServiceDiscovery
            .Setup(x => x.ResolveServiceEndpoint(
                GlobalFixture.RabbitMqInstance2.Name,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ServiceEndpoint
            {
                Host = GlobalFixture.RabbitMqInstance2.Host,
                Port = GlobalFixture.RabbitMqInstance2.Port
            }]);

        _mockDependenciesProvider
            .Setup(x => x.GetStaticSecretPath(GlobalFixture.RabbitMqInstance1.Name))
            .ReturnsAsync($"{GlobalFixture.AppNamespace}/{GlobalFixture.RabbitMqInstance1.Name}");

        _mockDependenciesProvider
            .Setup(x => x.GetStaticSecretPath(GlobalFixture.RabbitMqInstance2.Name))
            .ReturnsAsync($"{GlobalFixture.AppNamespace}/{GlobalFixture.RabbitMqInstance2.Name}");
    }

    private object AppSettings =>
        new
        {
            Vault = new
            {
                Address = GlobalFixture.Vault.VaultAddress,
                Token = GlobalFixture.Vault.Password,
                Scheme = "http",
                MountPoint = GlobalFixture.Vault.KvMountPoint
            },
            MessageQueues = new
            {
                Connections = new[]
                {
                    new
                    {
                        Name = Connection1Name,
                        Type = "rabbit-static",
                        ServiceName = GlobalFixture.RabbitMqInstance1.Name
                    },
                    new
                    {
                        Name = Connection2Name,
                        Type = "rabbit-static",
                        ServiceName = GlobalFixture.RabbitMqInstance2.Name
                    }
                },
                Consumers = new[]
                {
                    new
                    {
                        Name = "test-consumer-1",
                        ConnectionName = Connection1Name
                    },
                    new
                    {
                        Name = "test-consumer-2",
                        ConnectionName = Connection2Name
                    }
                }
            },
            AsyncInitializer = new
            {
                Enabled = true,
                Timeout = TimeSpan.FromMinutes(1)
            }
        };

    #endregion

    #region Test Methods

    [Test]
    public void WhenResolvingFirstProviderByConnectionNameThenShouldResolveProperKeyedService()
    {
        var provider1 = _sut.Services.GetRequiredKeyedService<IClientProvider<IConnection>>(Connection1Name);
        provider1.Should().NotBeNull("First RabbitMQ client provider should be registered");
    }

    [Test]
    public void WhenResolvingSecondProviderByConnectionNameThenProviderShouldNotBeNull()
    {
        var provider2 = _sut.Services.GetRequiredKeyedService<IClientProvider<IConnection>>(Connection2Name);
        provider2.Should().NotBeNull("Second RabbitMQ client provider should be registered");
    }

    [Test]
    public void WhenResolvingClientProvidersThenShouldNotReturnSameInstanceForFirstAndSecondConnection()
    {
        var provider1 = _sut.Services.GetRequiredKeyedService<IClientProvider<IConnection>>(Connection1Name);
        var provider2 = _sut.Services.GetRequiredKeyedService<IClientProvider<IConnection>>(Connection2Name);

        provider1.Should().NotBe(provider2, "Resolved providers should not reference same instance");
    }

    [Test]
    public void WhenResolvingSameProviderMultipleTimesThenShouldReturnSameInstance()
    {
        var provider1A = _sut.Services.GetRequiredKeyedService<IClientProvider<IConnection>>(Connection1Name);
        var provider1B = _sut.Services.GetRequiredKeyedService<IClientProvider<IConnection>>(Connection1Name);

        provider1A.Should().Be(provider1B, "Should return same provider instance for same connection name");
    }

    [Test]
    public void WhenAcquiringConnectionFromProvider1ThenShouldReturnValidConnection()
    {
        var provider1 = _sut.Services.GetRequiredKeyedService<IClientProvider<IConnection>>(Connection1Name);
        using var lease = provider1.Acquire();

        lease.Client.Should().NotBeNull("First provider should return valid connection");
        lease.Client.IsOpen.Should().BeTrue("Connection should be open");
    }

    [Test]
    public void WhenAcquiringConnectionFromProvider2ThenShouldReturnValidConnection()
    {
        var provider2 = _sut.Services.GetRequiredKeyedService<IClientProvider<IConnection>>(Connection2Name);
        using var lease = provider2.Acquire();

        lease.Client.Should().NotBeNull("Second provider should return valid connection");
        lease.Client.IsOpen.Should().BeTrue("Connection should be open");
    }

    [Test]
    public void WhenProvidersInitializedThenShouldUseCorrectCredentialsFromVault()
    {
        var provider1 = _sut.Services.GetRequiredKeyedService<IClientProvider<IConnection>>(Connection1Name);
        var provider2 = _sut.Services.GetRequiredKeyedService<IClientProvider<IConnection>>(Connection2Name);

        using var lease1 = provider1.Acquire();
        using var lease2 = provider2.Acquire();

        lease1.Client.Should().NotBeNull("Provider 1 should be initialized with valid connection");
        lease2.Client.Should().NotBeNull("Provider 2 should be initialized with valid connection");

        lease1.Client.IsOpen.Should().BeTrue("Connection 1 should be open");
        lease2.Client.IsOpen.Should().BeTrue("Connection 2 should be open");
    }

    #endregion
}
