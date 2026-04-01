using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Minnaloushe.Core.ClientProviders.Abstractions.VaultExtensions;
using Minnaloushe.Core.ClientProviders.Telegram.Tests.HelperClasses;
using Minnaloushe.Core.Toolbox.AsyncInitializer.Extensions;
using Minnaloushe.Core.Toolbox.TestHelpers;
using Minnaloushe.Core.VaultOptions.Vault;
using Telegram.Bot;

namespace Minnaloushe.Core.ClientProviders.Telegram.Tests.Unit;

[Category("Unit")]
public class TelegramClientProviderUnitTests
{
    #region Constants

    private const string BotKey1 = "bot1";
    private const string BotKey2 = "bot2";
    private const string BotToken1 = "123456:ABC-DEF1234ghIkl-zyx57W2v1u123ew11";
    private const string BotToken2 = "654321:XYZ-ABC9876def-rst43U8w9x876fg99";
    private const long ChatId1 = 123456789;
    private const long ChatId2 = 987654321;

    #endregion

    private TestHost _sut = null!;
    private Mock<ITelegramBotClientFactory> _mockBotFactory = null!;
    private Mock<ITelegramBotClient> _mockBotClient1 = null!;
    private Mock<ITelegramBotClient> _mockBotClient2 = null!;
    private Mock<IVaultOptionsLoader<TelegramOptions>> _mockVaultLoader = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        #region Prepare mocks

        _mockBotClient1 = new Mock<ITelegramBotClient>();
        _mockBotClient2 = new Mock<ITelegramBotClient>();

        _mockBotFactory = new Mock<ITelegramBotClientFactory>();
        _mockBotFactory
            .Setup(x => x.Create(It.Is<string>(token => token == BotToken1)))
            .Returns(_mockBotClient1.Object);
        _mockBotFactory
            .Setup(x => x.Create(It.Is<string>(token => token == BotToken2)))
            .Returns(_mockBotClient2.Object);

        _mockVaultLoader = new Mock<IVaultOptionsLoader<TelegramOptions>>();
        _mockVaultLoader
            .Setup(x => x.LoadAsync(It.IsAny<TelegramOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TelegramOptions opts, CancellationToken _) => opts);

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

                services.ConfigureAsyncInitializers();

                // Register Telegram client providers
                services.AddKeyedTelegramClientProviders(configuration)
                    .WithDependency<ITelegramWrapper, TelegramWrapper>(
                        (svc, key) => svc.AddKeyedAsyncInitializer<TelegramWrapper>(key)
                        );

                services.AddSingleton(_mockBotFactory.Object);
                services.AddSingleton(_mockVaultLoader.Object);
            },
            beforeStart: async host =>
            {
                await host.InvokeAsyncInitializers();
            },
            startHost: false);

        #endregion
    }

    private static object AppSettings =>
        new
        {
            Telegram = new
            {
                bot1 = new
                {
                    BotToken = BotToken1,
                    ChatId = ChatId1
                },
                bot2 = new
                {
                    BotToken = BotToken2,
                    ChatId = ChatId2
                }
            },
            AsyncInitializer = new
            {
                Enabled = true,
                Timeout = TimeSpan.FromMinutes(1)
            }
        };

    [Test]
    public void WhenKeyedDependencyResolvedThenShouldResolveWithProperClientProvider()
    {
        var wrapper1 = _sut.Services.GetRequiredKeyedService<ITelegramWrapper>(BotKey1);
        var provider1 = _sut.Services.GetRequiredKeyedService<ITelegramClientProvider>(BotKey1);

        wrapper1.ClientProvider.Should().Be(provider1, "Resolved wrapper should reference the correct client provider");
        wrapper1.InitializationCompleted.Should().BeTrue("Wrapper should have completed initialization");

        var wrapper2 = _sut.Services.GetRequiredKeyedService<ITelegramWrapper>(BotKey2);
        var provider2 = _sut.Services.GetRequiredKeyedService<ITelegramClientProvider>(BotKey2);

        wrapper2.ClientProvider.Should().Be(provider2, "Resolved wrapper should reference the correct client provider");
        wrapper2.InitializationCompleted.Should().BeTrue("Wrapper should have completed initialization");
    }

    [Test]
    public void WhenResolvingFirstProviderByBotKeyThenProvider1ShouldNotBeNull()
    {
        var provider1 = _sut.Services.GetKeyedService<ITelegramClientProvider>(BotKey1);
        provider1.Should().NotBeNull("First Telegram client provider should be registered");
    }

    [Test]
    public void WhenResolvingSecondProviderByBotKeyThenProvider2ShouldNotBeNull()
    {
        var provider2 = _sut.Services.GetKeyedService<ITelegramClientProvider>(BotKey2);
        provider2.Should().NotBeNull("Second Telegram client provider should be registered");
    }

    [Test]
    public void WhenResolvingClientProvidersThenShouldNotReturnSameInstanceForFirstAndSecondBot()
    {
        var provider1 = _sut.Services.GetKeyedService<ITelegramClientProvider>(BotKey1);
        var provider2 = _sut.Services.GetKeyedService<ITelegramClientProvider>(BotKey2);

        provider1.Should().NotBe(provider2, "Resolved providers should not reference same instance");
    }

    [Test]
    public void WhenResolvingClientProvidersThenProvidersShouldReferenceDifferentBotClients()
    {
        var provider1 = _sut.Services.GetRequiredKeyedService<ITelegramClientProvider>(BotKey1);
        var provider2 = _sut.Services.GetRequiredKeyedService<ITelegramClientProvider>(BotKey2);

        var client1 = provider1.Client;
        var client2 = provider2.Client;

        client1.Should().NotBe(client2, "Providers should reference different bot clients");
        client1.Should().Be(_mockBotClient1.Object, "First provider should return mock bot client 1");
        client2.Should().Be(_mockBotClient2.Object, "Second provider should return mock bot client 2");
    }

    [Test]
    public void WhenAccessingBot1ClientThenShouldReturnCorrectClient()
    {
        var provider1 = _sut.Services.GetRequiredKeyedService<ITelegramClientProvider>(BotKey1);
        var client1 = provider1.Client;

        client1.Should().NotBeNull("Bot client 1 should be accessible");
        client1.Should().Be(_mockBotClient1.Object, "Should return the first mocked bot client");
    }

    [Test]
    public void WhenAccessingBot2ClientThenShouldReturnCorrectClient()
    {
        var provider2 = _sut.Services.GetRequiredKeyedService<ITelegramClientProvider>(BotKey2);
        var client2 = provider2.Client;

        client2.Should().NotBeNull("Bot client 2 should be accessible");
        client2.Should().Be(_mockBotClient2.Object, "Should return the second mocked bot client");
    }

    [Test]
    public void WhenResolvingSameProviderMultipleTimesThenShouldReturnSameInstance()
    {
        var provider1A = _sut.Services.GetRequiredKeyedService<ITelegramClientProvider>(BotKey1);
        var provider1B = _sut.Services.GetRequiredKeyedService<ITelegramClientProvider>(BotKey1);

        provider1A.Should().Be(provider1B, "Should return same provider instance for same bot key");
    }

    [Test]
    public void WhenBotFactoryCalledThenShouldBeCalledWithCorrectTokens()
    {
        // Accessing clients should have triggered factory calls during initialization
        _ = _sut.Services.GetRequiredKeyedService<ITelegramClientProvider>(BotKey1).Client;
        _ = _sut.Services.GetRequiredKeyedService<ITelegramClientProvider>(BotKey2).Client;

        _mockBotFactory.Verify(x => x.Create(BotToken1), Times.Once(), "Factory should be called once with bot token 1");
        _mockBotFactory.Verify(x => x.Create(BotToken2), Times.Once(), "Factory should be called once with bot token 2");
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _sut.DisposeAsync();
    }
}