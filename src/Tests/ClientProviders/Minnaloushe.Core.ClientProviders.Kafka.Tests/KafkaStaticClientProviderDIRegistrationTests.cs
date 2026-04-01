using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Minnaloushe.Core.ClientProviders.Abstractions;
using Minnaloushe.Core.ClientProviders.Abstractions.ClientLease;
using Minnaloushe.Core.ClientProviders.Kafka.Vault;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Extensions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Kafka.Vault.Extensions;
using Minnaloushe.Core.ServiceDiscovery.Abstractions;
using Minnaloushe.Core.ServiceDiscovery.Extensions;
using Minnaloushe.Core.Toolbox.AsyncInitializer.Extensions;
using Minnaloushe.Core.Toolbox.TestHelpers;
using VaultSharp;

namespace Minnaloushe.Core.ClientProviders.Kafka.Tests;

/// <summary>
/// Unit tests for Kafka client provider DI registration (Vault/Static version).
/// Verifies that AddVaultKafkaClientProviders correctly registers:
/// - IKafkaConsumerClientProvider instances keyed by connection name
/// - IKafkaAdminClientProvider instances keyed by connection name
/// - Each connection has ServiceName configured (credentials come from Vault)
/// - Providers are singletons
/// 
/// Note: Connections are only registered when they have at least one consumer configured.
/// Note: This tests the Vault version where credentials are NOT in config (fetched from Vault).
/// Note: This test verifies registration only, not initialization (which would require Vault).
/// </summary>
[TestFixture]
[Category("Unit")]
public class KafkaStaticClientProviderDIRegistrationTests
{
    #region Fixture members

    #region Constants

    private const string Connection1Name = "kafka-conn1";
    private const string Connection2Name = "kafka-conn2";

    private const string ServiceName1 = "kafka-service-1";
    private const string ServiceName2 = "kafka-service-2";

    #endregion

    private TestHost _sut = null!;
    private readonly Mock<IServiceDiscoveryService> _mockServiceDiscovery = new();
    private readonly Mock<IClientProvider<IVaultClient>> _mockVaultClientProvider = new();

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

                services.AddServiceDiscovery();

                services.AddSingleton(configuration);
                services.AddSingleton(_mockServiceDiscovery.Object);
                services.AddSingleton(_mockVaultClientProvider.Object);

                services.ConfigureAsyncInitializers();

                services.AddMessageQueues(configuration)
                    .AddVaultKafkaClientProviders()
                    .Build();
            },
            // Skip async initializer invocation since it would try to connect to Vault
            startHost: false);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _sut.DisposeAsync();
    }

    private void SetupMocks()
    {
        // Mock service discovery (not used in registration, but required by constructor)
        _mockServiceDiscovery
            .Setup(x => x.ResolveServiceEndpoint(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Mock vault client provider (required by constructor)
        var mockVaultClient = new Mock<IVaultClient>();
        var holder = new ClientHolder<IVaultClient>(mockVaultClient.Object, 0); // epoch 0 = not initialized
        var lease = new ManagedClientLease<IVaultClient>(holder);

        _mockVaultClientProvider
            .Setup(x => x.Acquire())
            .Returns(lease);
    }

    private static object AppSettings =>
        new
        {
            MessageQueues = new
            {
                Connections = new[]
                {
                    new
                    {
                        Name = Connection1Name,
                        Type = "kafka-static",
                        ServiceName = ServiceName1
                    },
                    new
                    {
                        Name = Connection2Name,
                        Type = "kafka-static",
                        ServiceName = ServiceName2
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

    [Test]
    public void GetRequiredKeyedService_WhenResolvingFirstProviderByConnectionName_ThenShouldResolveProperKeyedService()
    {
        // Arrange

        // Act
        var provider1 = _sut.Services.GetRequiredKeyedService<IKafkaConsumerClientProvider>(Connection1Name);

        // Assert
        provider1.Should().NotBeNull("First Kafka client provider should be registered");
    }

    [Test]
    public void GetRequiredKeyedService_WhenResolvingSecondProviderByConnectionName_ThenProviderShouldNotBeNull()
    {
        // Arrange

        // Act
        var provider2 = _sut.Services.GetRequiredKeyedService<IKafkaConsumerClientProvider>(Connection2Name);

        // Assert
        provider2.Should().NotBeNull("Second Kafka client provider should be registered");
    }

    [Test]
    public void GetRequiredKeyedService_WhenResolvingClientProviders_ThenShouldNotReturnSameInstanceForFirstAndSecondConnection()
    {
        // Arrange

        // Act
        var provider1 = _sut.Services.GetRequiredKeyedService<IKafkaConsumerClientProvider>(Connection1Name);
        var provider2 = _sut.Services.GetRequiredKeyedService<IKafkaConsumerClientProvider>(Connection2Name);

        // Assert
        provider1.Should().NotBe(provider2, "Resolved providers should not reference same instance");
    }

    [Test]
    public void GetRequiredKeyedService_WhenResolvingSameProviderMultipleTimes_ThenShouldReturnSameInstance()
    {
        // Arrange

        // Act
        var provider1A = _sut.Services.GetRequiredKeyedService<IKafkaConsumerClientProvider>(Connection1Name);
        var provider1B = _sut.Services.GetRequiredKeyedService<IKafkaConsumerClientProvider>(Connection1Name);

        // Assert
        provider1A.Should().Be(provider1B, "Should return same provider instance for same connection name");
    }

    [Test]
    public void GetKeyedService_WhenResolvingAdminProvider_ThenShouldNotBeNull()
    {
        // Arrange

        // Act
        var adminProvider1 = _sut.Services.GetKeyedService<IKafkaAdminClientProvider>(Connection1Name);

        // Assert
        adminProvider1.Should().NotBeNull("Kafka admin client provider should be registered");
    }

    [Test]
    public void GetKeyedService_WhenResolvingAdminProvidersForDifferentConnections_ThenShouldReturnDifferentInstances()
    {
        // Arrange

        // Act
        var adminProvider1 = _sut.Services.GetKeyedService<IKafkaAdminClientProvider>(Connection1Name);
        var adminProvider2 = _sut.Services.GetKeyedService<IKafkaAdminClientProvider>(Connection2Name);

        // Assert
        adminProvider1.Should().NotBe(adminProvider2, "Admin providers for different connections should not be the same instance");
    }
}
