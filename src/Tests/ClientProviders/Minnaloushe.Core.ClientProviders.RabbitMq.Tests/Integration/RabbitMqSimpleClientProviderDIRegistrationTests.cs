using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Minnaloushe.Core.ClientProviders.Abstractions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Extensions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.RabbitMq.Extensions;
using Minnaloushe.Core.Toolbox.AsyncInitializer.Extensions;
using Minnaloushe.Core.Toolbox.TestHelpers;
using RabbitMQ.Client;

namespace Minnaloushe.Core.ClientProviders.RabbitMq.Tests.Integration;

/// <summary>
/// Integration tests for RabbitMQ simple client provider DI registration (non-Vault).
/// Verifies that AddRabbitMqClientProviders correctly registers:
/// - IRabbitMqClientProvider instances keyed by connection name
/// - Each connection uses its own credentials from config (Username/Password required)
/// - Providers are singletons
/// - Providers can acquire client connections correctly
/// 
/// Note: Connections are only registered when they have at least one consumer configured.
/// Note: Uses TestContainers to spin up real RabbitMQ instances.
/// </summary>
[TestFixture]
[Category("Integration")]
[Category("TestContainers")]
public class RabbitMqSimpleClientProviderDIRegistrationTests
{
    #region Constants

    private const string Connection1Name = "rabbit-simple-conn1";
    private const string Connection2Name = "rabbit-simple-conn2";

    #endregion

    #region Fields/Members/Constants

    private TestHost _sut = null!;

    #endregion

    #region Setup/Teardown

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
                    .AddRabbitMqClientProviders()
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

    private object AppSettings =>
        new
        {
            MessageQueues = new
            {
                Connections = new[]
                {
                    new
                    {
                        Name = Connection1Name,
                        Type = "rabbitmq",
                        Host = GlobalFixture.RabbitMqInstance1.Host,
                        Port = GlobalFixture.RabbitMqInstance1.Port,
                        Username = GlobalFixture.RabbitMqInstance1.Username,
                        Password = GlobalFixture.RabbitMqInstance1.Password
                    },
                    new
                    {
                        Name = Connection2Name,
                        Type = "rabbitmq",
                        Host = GlobalFixture.RabbitMqInstance2.Host,
                        Port = GlobalFixture.RabbitMqInstance2.Port,
                        Username = GlobalFixture.RabbitMqInstance2.Username,
                        Password = GlobalFixture.RabbitMqInstance2.Password
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
    public void WhenResolvingFirstProviderByConnectionNameThenProviderShouldNotBeNull()
    {
        var provider1 = _sut.Services.GetKeyedService<IClientProvider<IConnection>>(Connection1Name);
        provider1.Should().NotBeNull("First RabbitMQ client provider should be registered");
    }

    [Test]
    public void WhenResolvingSecondProviderByConnectionNameThenProviderShouldNotBeNull()
    {
        var provider2 = _sut.Services.GetKeyedService<IClientProvider<IConnection>>(Connection2Name);
        provider2.Should().NotBeNull("Second RabbitMQ client provider should be registered");
    }

    [Test]
    public void WhenResolvingClientProvidersThenShouldNotReturnSameInstanceForFirstAndSecondConnection()
    {
        var provider1 = _sut.Services.GetKeyedService<IClientProvider<IConnection>>(Connection1Name);
        var provider2 = _sut.Services.GetKeyedService<IClientProvider<IConnection>>(Connection2Name);

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
    public void WhenProvidersInitializedThenShouldUseCorrectCredentials()
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
