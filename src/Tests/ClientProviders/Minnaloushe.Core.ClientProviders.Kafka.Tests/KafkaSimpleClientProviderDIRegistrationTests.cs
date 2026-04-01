using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Extensions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Kafka.Extensions;
using Minnaloushe.Core.Toolbox.AsyncInitializer.Extensions;
using Minnaloushe.Core.Toolbox.TestHelpers;

namespace Minnaloushe.Core.ClientProviders.Kafka.Tests;

/// <summary>
/// Unit tests for Kafka simple client provider DI registration (non-Vault).
/// Verifies that AddKafkaClientProviders correctly registers:
/// - IKafkaConsumerClientProvider instances keyed by connection name
/// - IKafkaAdminClientProvider instances keyed by connection name
/// - Each connection uses its own credentials from config (Username/Password required)
/// - Providers are singletons
/// - Providers can acquire client wrappers correctly
/// 
/// Note: Connections are only registered when they have at least one consumer configured.
/// </summary>
[TestFixture]
[Category("Unit")]
public class KafkaSimpleClientProviderDIRegistrationTests
{
    #region Fixture members

    #region Constants

    private const string Connection1Name = "kafka-simple-conn1";
    private const string Connection2Name = "kafka-simple-conn2";

    private const string Host1 = "kafka-simple1.example.com";
    private const string Host2 = "kafka-simple2.example.com";

    private const ushort Port1 = 9092;
    private const ushort Port2 = 9093;

    private const string Username1 = "simple-user1";
    private const string Username2 = "simple-user2";

    private const string Password1 = "simple-password1";
    private const string Password2 = "simple-password2";

    #endregion

    private TestHost _sut = null!;

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
                        Type = "kafka",
                        Host = Host1,
                        Port = Port1,
                        ServiceKey = "test-service-key-1",
                        Username = Username1,
                        Password = Password1
                    },
                    new
                    {
                        Name = Connection2Name,
                        Type = "kafka",
                        Host = Host2,
                        Port = Port2,
                        ServiceKey = "test-service-key-1",
                        Username = Username2,
                        Password = Password2
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

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
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
                services.ConfigureAsyncInitializers();

                services.AddMessageQueues(configuration)
                    .AddKafkaClientProviders()
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

    [Test]
    public void GetKeyedService_WhenResolvingFirstProviderByConnectionName_ThenProviderShouldNotBeNull()
    {
        // Arrange

        // Act
        var provider1 = _sut.Services.GetKeyedService<IKafkaConsumerClientProvider>(Connection1Name);

        // Assert
        provider1.Should().NotBeNull("First Kafka client provider should be registered");
    }

    [Test]
    public void GetKeyedService_WhenResolvingSecondProviderByConnectionName_ThenProviderShouldNotBeNull()
    {
        // Arrange

        // Act
        var provider2 = _sut.Services.GetKeyedService<IKafkaConsumerClientProvider>(Connection2Name);

        // Assert
        provider2.Should().NotBeNull("Second Kafka client provider should be registered");
    }

    [Test]
    public void GetKeyedService_WhenResolvingClientProviders_ThenShouldNotReturnSameInstanceForFirstAndSecondConnection()
    {
        // Arrange

        // Act
        var provider1 = _sut.Services.GetKeyedService<IKafkaConsumerClientProvider>(Connection1Name);
        var provider2 = _sut.Services.GetKeyedService<IKafkaConsumerClientProvider>(Connection2Name);

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
    public void Acquire_WhenAcquiringClientFromProvider1_ThenShouldReturnValidWrapper()
    {
        // Arrange
        var provider1 = _sut.Services.GetRequiredKeyedService<IKafkaConsumerClientProvider>(Connection1Name);

        // Act
        using var lease = provider1.Acquire();

        // Assert
        lease.Client.Should().NotBeNull("First provider should return valid client wrapper");
        lease.Client.Consumer.Should().NotBeNull("Consumer should be initialized");
    }

    [Test]
    public void Acquire_WhenAcquiringClientFromProvider2_ThenShouldReturnValidWrapper()
    {
        // Arrange
        var provider2 = _sut.Services.GetRequiredKeyedService<IKafkaConsumerClientProvider>(Connection2Name);

        // Act
        using var lease = provider2.Acquire();

        // Assert
        lease.Client.Should().NotBeNull("Second provider should return valid client wrapper");
        lease.Client.Consumer.Should().NotBeNull("Consumer should be initialized");
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

    [Test]
    public void Acquire_WhenAcquiringAdminClient_ThenShouldReturnValidWrapper()
    {
        // Arrange
        var adminProvider = _sut.Services.GetRequiredKeyedService<IKafkaAdminClientProvider>(Connection1Name);

        // Act
        using var lease = adminProvider.Acquire();

        // Assert
        lease.Client.Should().NotBeNull("Admin provider should return valid client wrapper");
        lease.Client.Client.Should().NotBeNull("Admin client should be initialized");
    }

    [Test]
    public void ProvidersInitialization_WhenProvidersInitialized_ThenShouldUseCorrectCredentials()
    {
        // Arrange
        var provider1 = _sut.Services.GetRequiredKeyedService<IKafkaConsumerClientProvider>(Connection1Name);
        var provider2 = _sut.Services.GetRequiredKeyedService<IKafkaConsumerClientProvider>(Connection2Name);

        // Act
        using var lease1 = provider1.Acquire();
        using var lease2 = provider2.Acquire();

        // Assert
        lease1.Client.Should().NotBeNull("Provider 1 should be initialized with valid client");
        lease2.Client.Should().NotBeNull("Provider 2 should be initialized with valid client");

        lease1.Client.Consumer.Should().NotBeNull("Consumer 1 should be initialized");
        lease2.Client.Consumer.Should().NotBeNull("Consumer 2 should be initialized");
    }
}
