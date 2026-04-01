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
public class TelegramClientProviderSingleVaultInitialisationTests
{
    #region Constants

    private const string BotToken1 = "123456:ABC-DEF1234ghIkl-zyx57W2v1u123ew11";
    private const long ChatId1 = 123456789;
    // For VaultSharp, use the logical path without /data/ - the library adds it automatically
    private readonly string _vaultSecretPath = Helpers.UniqueString("telegram/single-bot");
    // For direct HTTP API calls, use the full path with /data/

    #endregion

    private TestHost _sut = null!;
    private Mock<ITelegramBotClientFactory> _mockBotFactory = null!;
    private Mock<ITelegramBotClient> _mockBotClient = null!;
    private readonly Mock<IServiceDiscoveryService> _mockServiceDiscovery = new();

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {

        await InitializeVaultTelegramSecrets();

        #region Prepare mocks

        _mockBotClient = new Mock<ITelegramBotClient>();

        _mockBotFactory = new Mock<ITelegramBotClientFactory>();
        _mockBotFactory
            .Setup(x => x.Create(It.Is<string>(token => token == BotToken1)))
            .Returns(_mockBotClient.Object);

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

                // Register single Telegram client provider
                services.AddTelegramClientProvider();

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
                VaultPath = _vaultSecretPath
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
    public void WhenResolvingProviderThenProviderShouldNotBeNull()
    {
        var provider = _sut.Services.GetService<ITelegramClientProvider>();
        provider.Should().NotBeNull("Telegram client provider should be registered");
    }

    [Test]
    public void WhenAccessingBotClientThenShouldReturnCorrectClient()
    {
        var provider = _sut.Services.GetRequiredService<ITelegramClientProvider>();
        var client = provider.Client;

        client.Should().NotBeNull("Bot client should be accessible");
        client.Should().Be(_mockBotClient.Object, "Should return the mocked bot client");
    }

    [Test]
    public void WhenBotInitializedFromVaultThenFactoryShouldBeCalledWithCorrectToken()
    {
        // Access client to ensure initialization has completed
        var provider = _sut.Services.GetRequiredService<ITelegramClientProvider>();
        _ = provider.Client;

        _mockBotFactory.Verify(
            x => x.Create(BotToken1),
            Times.Once(),
            "Factory should be called once with the token retrieved from Vault");
    }

    [Test]
    public void WhenResolvingSameProviderMultipleTimesThenShouldReturnSameInstance()
    {
        var provider1 = _sut.Services.GetRequiredService<ITelegramClientProvider>();
        var provider2 = _sut.Services.GetRequiredService<ITelegramClientProvider>();

        provider1.Should().Be(provider2, "Should return same provider instance");
    }

    [Test]
    public void WhenAccessingClientMultipleTimesThenShouldReturnSameClientInstance()
    {
        var provider = _sut.Services.GetRequiredService<ITelegramClientProvider>();
        var client1 = provider.Client;
        var client2 = provider.Client;

        client1.Should().Be(client2, "Client property should return the same instance");
    }

    [Test]
    public void WhenVaultSecretsInitializedThenProviderShouldHaveCorrectChatId()
    {
        var options = _sut.Services.GetRequiredService<IResolvedOptions<TelegramOptions>>();

        options.Value.ChatId.Should().Be(ChatId1, "ChatId should be loaded from Vault");
    }

    [Test]
    public void WhenAsyncInitializerInvokedThenProviderShouldBeInitialized()
    {
        // The provider should already be initialized during OneTimeSetUp
        // This test verifies that the initialization completed successfully
        var provider = _sut.Services.GetRequiredService<ITelegramClientProvider>();

        // Access the client to ensure initialization
        var client = provider.Client;

        client.Should().NotBeNull("Client should be initialized after async initializer completes");
        _mockBotFactory.Verify(x => x.Create(It.IsAny<string>()), Times.Once());
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _sut.DisposeAsync();
    }
}
