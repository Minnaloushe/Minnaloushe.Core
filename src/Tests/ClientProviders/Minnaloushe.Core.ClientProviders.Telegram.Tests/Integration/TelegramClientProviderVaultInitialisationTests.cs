using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Minnaloushe.Core.ServiceDiscovery.Abstractions;
using Minnaloushe.Core.ServiceDiscovery.Routines;
using Minnaloushe.Core.Tests.Helpers;
using Minnaloushe.Core.Toolbox.AsyncInitializer.Extensions;
using Minnaloushe.Core.Toolbox.TestHelpers;
using Minnaloushe.Core.VaultOptions.Extensions;
using Minnaloushe.Core.VaultService.Extensions;
using Telegram.Bot;

namespace Minnaloushe.Core.ClientProviders.Telegram.Tests.Integration;

[Category("Integration")]
[Category("TestContainers")]
public class TelegramClientProviderVaultInitialisationTests
{
    #region Constants

    private const string BotKey1 = "bot1";
    private const string BotToken1 = "123456:ABC-DEF1234ghIkl-zyx57W2v1u123ew11";
    private const long ChatId1 = 123456789;
    // For VaultSharp, use the logical path without /data/ - the library adds it automatically
    private readonly string _vaultSecretPath = Helpers.UniqueString("telegram/bot1");
    // For direct HTTP API calls, use the full path with /data/

    #endregion

    private TestHost _sut = null!;
    private Mock<ITelegramBotClientFactory> _mockBotFactory = null!;
    private Mock<ITelegramBotClient> _mockBotClient1 = null!;
    private readonly Mock<IServiceDiscoveryService> _mockServiceDiscovery = new();

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        await InitializeVaultTelegramSecrets();

        #region Prepare mocks

        _mockBotClient1 = new Mock<ITelegramBotClient>();

        _mockBotFactory = new Mock<ITelegramBotClientFactory>();
        _mockBotFactory
            .Setup(x => x.Create(It.Is<string>(token => token == BotToken1)))
            .Returns(_mockBotClient1.Object);

        #endregion

        #region Build TestHost using AppConfig

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
                services.AddSingleton<IInfrastructureConventionProvider, InfrastructureConventionProvider>();

                services.ConfigureAsyncInitializers();

                // Register Vault client provider
                services.AddVaultClientProvider();
                services.AddVaultStoredOptions();

                // Register Telegram client providers
                services.AddKeyedTelegramClientProviders(configuration);


                // Register mock bot factory
                services.AddSingleton(_mockBotFactory.Object);
            },
            beforeStart: async host =>
            {
                await host.InvokeAsyncInitializers();
            },
            startHost: false);

        #endregion
    }

    private object AppSettings =>
        new
        {
            Vault = new
            {
                Address = GlobalFixture.Vault.VaultAddress,
                Token = GlobalFixture.Vault.Password,
                Scheme = "http",
                ServiceName = GlobalFixture.Vault.Name,
                MountPoint = GlobalFixture.Vault.KvMountPoint
            },
            Telegram = new
            {
                bot1 = new
                {
                    VaultPath = _vaultSecretPath
                }
            },
            AsyncInitializer = new
            {
                Enabled = true,
                Timeout = TimeSpan.FromMinutes(1)
            }
        };

    #region Helper methods

    private async Task InitializeVaultTelegramSecrets()
    {
        var secretData = new
        {
            data = new
            {
                BotToken = BotToken1,
                ChatId = ChatId1.ToString()
            }
        };

        await GlobalFixture.Vault.ConfigureStaticSecretFromObject(_vaultSecretPath, secretData);
    }

    #endregion

    [Test]
    public void WhenResolvingProviderByBotKeyThenProviderShouldNotBeNull()
    {
        var provider = _sut.Services.GetKeyedService<ITelegramClientProvider>(BotKey1);
        provider.Should().NotBeNull("Telegram client provider should be registered");
    }

    [Test]
    public void WhenAccessingBotClientThenShouldReturnCorrectClient()
    {
        var provider = _sut.Services.GetRequiredKeyedService<ITelegramClientProvider>(BotKey1);
        var client = provider.Client;

        client.Should().NotBeNull("Bot client should be accessible");
        client.Should().Be(_mockBotClient1.Object, "Should return the mocked bot client");
    }

    [Test]
    public void WhenBotInitializedFromVaultThenFactoryShouldBeCalledWithCorrectToken()
    {
        // Access client to ensure initialization has completed
        var provider = _sut.Services.GetRequiredKeyedService<ITelegramClientProvider>(BotKey1);
        _ = provider.Client;

        _mockBotFactory.Verify(
            x => x.Create(BotToken1),
            Times.Once(),
            "Factory should be called once with the token retrieved from Vault");
    }

    [Test]
    public void WhenResolvingSameProviderMultipleTimesThenShouldReturnSameInstance()
    {
        var provider1A = _sut.Services.GetRequiredKeyedService<ITelegramClientProvider>(BotKey1);
        var provider1B = _sut.Services.GetRequiredKeyedService<ITelegramClientProvider>(BotKey1);

        provider1A.Should().Be(provider1B, "Should return same provider instance for same bot key");
    }

    [Test]
    public void WhenAccessingClientMultipleTimesThenShouldReturnSameClientInstance()
    {
        var provider = _sut.Services.GetRequiredKeyedService<ITelegramClientProvider>(BotKey1);
        var client1 = provider.Client;
        var client2 = provider.Client;

        client1.Should().Be(client2, "Client property should return the same instance");
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _sut.DisposeAsync();
    }
}