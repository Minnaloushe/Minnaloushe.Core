using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Minnaloushe.Core.ServiceDiscovery.Abstractions;
using Minnaloushe.Core.ServiceDiscovery.Routines;
using Minnaloushe.Core.Tests.Helpers;
using Minnaloushe.Core.Toolbox.AsyncInitializer.Extensions;
using Minnaloushe.Core.Toolbox.TestHelpers;
using Minnaloushe.Core.VaultOptions.Extensions;
using Minnaloushe.Core.VaultOptions.ResolvedOptions;
using Minnaloushe.Core.VaultService.Extensions;
using Telegram.Bot;

namespace Minnaloushe.Core.ClientProviders.Telegram.Tests.Integration;

[Category("Integration")]
[Category("TestContainers")]
public class TelegramClientProviderMultipleVaultInitialisationTests
{
    #region Constants

    private const string BotToken1 = "123456:ABC-DEF1234ghIkl-zyx57W2v1u123ew11";
    private const string BotToken2 = "654321:ABC-DEF1234ghIkl-zyx57W2v1u123ew11";
    private const string BotToken3 = "444444:ABC-DEF1234ghIkl-zyx57W2v1u123ew11";
    private const long ChatId1 = 123456789;
    private const long ChatId2 = 987654321;
    private const long ChatId3 = 444444444;
    // For VaultSharp, use the logical path without /data/ - the library adds it automatically
    private readonly string _vaultSecretPath1 = Helpers.UniqueString("telegram/bot1");
    private readonly string _vaultSecretPath2 = Helpers.UniqueString("telegram/bot2");
    // For direct HTTP API calls, use the full path with /data/

    #endregion

    private TestHost _sut = null!;
    private Mock<ITelegramBotClientFactory> _mockBotFactory = null!;
    private Mock<ITelegramBotClient> _mockBotClient1 = null!;
    private Mock<ITelegramBotClient> _mockBotClient2 = null!;
    private Mock<ITelegramBotClient> _mockBotClient3 = null!;
    private readonly Mock<IServiceDiscoveryService> _mockServiceDiscovery = new();

    [SetUp]
    public void SetUp()
    {
        _mockServiceDiscovery.Reset();
    }

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {

        await InitializeVaultTelegramSecrets();

        #region Prepare mocks

        _mockBotClient1 = new Mock<ITelegramBotClient>();
        _mockBotClient2 = new Mock<ITelegramBotClient>();
        _mockBotClient3 = new Mock<ITelegramBotClient>();

        _mockBotFactory = new Mock<ITelegramBotClientFactory>();
        _mockBotFactory
            .Setup(x => x.Create(It.Is<string>(token => token == BotToken1)))
            .Returns(_mockBotClient1.Object);
        _mockBotFactory
            .Setup(x => x.Create(It.Is<string>(token => token == BotToken2)))
            .Returns(_mockBotClient2.Object);
        _mockBotFactory
            .Setup(x => x.Create(It.Is<string>(token => token == BotToken3)))
            .Returns(_mockBotClient3.Object);

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

                // Register keyed Telegram client providers
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
                    VaultPath = _vaultSecretPath1
                },
                bot2 = new
                {
                    VaultPath = _vaultSecretPath2
                },
                bot3 = new
                {
                    BotToken = BotToken3,
                    ChatId = ChatId3
                },
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
        var secretData1 = new
        {
            data = new
            {
                BotToken = BotToken1,
                ChatId = ChatId1.ToString()
            }
        };

        await GlobalFixture.Vault.ConfigureStaticSecretFromObject(_vaultSecretPath1, secretData1);

        var secretData2 = new
        {
            data = new
            {
                BotToken = BotToken2,
                ChatId = ChatId2.ToString()
            }
        };

        await GlobalFixture.Vault.ConfigureStaticSecretFromObject(_vaultSecretPath2, secretData2);
    }

    #endregion

    [Test]
    public void WhenResolvingKeyedProvidersThenProvidersShouldNotBeNull()
    {
        var provider1 = _sut.Services.GetKeyedService<ITelegramClientProvider>("bot1");
        var provider2 = _sut.Services.GetKeyedService<ITelegramClientProvider>("bot2");
        var provider3 = _sut.Services.GetKeyedService<ITelegramClientProvider>("bot3");

        provider1.Should().NotBeNull("First Telegram client provider should be registered");
        provider2.Should().NotBeNull("Second Telegram client provider should be registered");
        provider3.Should().NotBeNull("Third Telegram client provider should be registered");
    }

    [Test]
    public void WhenAccessingBotClientsThenShouldReturnCorrectClients()
    {
        var provider1 = _sut.Services.GetRequiredKeyedService<ITelegramClientProvider>("bot1");
        var provider2 = _sut.Services.GetRequiredKeyedService<ITelegramClientProvider>("bot2");
        var provider3 = _sut.Services.GetRequiredKeyedService<ITelegramClientProvider>("bot3");

        var client1 = provider1.Client;
        var client2 = provider2.Client;
        var client3 = provider3.Client;

        client1.Should().Be(_mockBotClient1.Object, "Should return the first mocked bot client");
        client2.Should().Be(_mockBotClient2.Object, "Should return the second mocked bot client");
        client3.Should().Be(_mockBotClient3.Object, "Should return the third mocked bot client");
    }

    [Test]
    public void WhenBotInitializedFromVaultThenFactoryShouldBeCalledWithCorrectTokens()
    {
        // Access clients to ensure initialization has completed
        _ = _sut.Services.GetRequiredKeyedService<ITelegramClientProvider>("bot1").Client;
        _ = _sut.Services.GetRequiredKeyedService<ITelegramClientProvider>("bot2").Client;

        _mockBotFactory.Verify(x => x.Create(BotToken1), Times.Once(), "Factory should be called once with token for bot1");
        _mockBotFactory.Verify(x => x.Create(BotToken2), Times.Once(), "Factory should be called once with token for bot2");
    }

    [Test]
    public void WhenResolvingSameProviderMultipleTimesThenShouldReturnSameInstance()
    {
        var provider1A = _sut.Services.GetRequiredKeyedService<ITelegramClientProvider>("bot1");
        var provider1B = _sut.Services.GetRequiredKeyedService<ITelegramClientProvider>("bot1");

        provider1A.Should().Be(provider1B, "Should return same provider instance for same bot key");
    }

    [Test]
    public void WhenAccessingClientMultipleTimesThenShouldReturnSameClientInstance()
    {
        var provider = _sut.Services.GetRequiredKeyedService<ITelegramClientProvider>("bot1");
        var client1 = provider.Client;
        var client2 = provider.Client;

        client1.Should().Be(client2, "Client property should return the same instance");
    }

    [Test]
    public void WhenVaultSecretsInitializedThenProvidersShouldHaveCorrectChatIds()
    {
        var keyed = _sut.Services.GetRequiredService<IResolvedKeyedOptions<TelegramOptions>>();

        var opts1 = keyed.Get("bot1")!;
        var opts2 = keyed.Get("bot2")!;

        opts1.Value.ChatId.Should().Be(ChatId1, "ChatId for bot1 should be loaded from Vault");
        opts2.Value.ChatId.Should().Be(ChatId2, "ChatId for bot2 should be loaded from Vault");
    }

    [Test]
    public void WhenAsyncInitializerInvokedThenProvidersShouldBeInitialized()
    {
        // The providers should already be initialized during OneTimeSetUp
        // This test verifies that the initialization completed successfully
        var provider1 = _sut.Services.GetRequiredKeyedService<ITelegramClientProvider>("bot1");
        var client = provider1.Client;

        client.Should().NotBeNull("Client should be initialized after async initializer completes");
        _mockBotFactory.Verify(x => x.Create(It.IsAny<string>()), Times.Exactly(3));
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _sut.DisposeAsync();
    }
}
