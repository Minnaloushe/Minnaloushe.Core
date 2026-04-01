using DotNet.Testcontainers.Networks;
using Minnaloushe.Core.Tests.Helpers;
using Minnaloushe.Core.Tests.Helpers.ContainerWrappers;

namespace Minnaloushe.Core.ClientProviders.Telegram.Tests.Integration;

[SetUpFixture]
public class GlobalFixture
{
    public static INetwork Network { get; private set; } = null!;
    public static VaultContainerWrapper Vault { get; private set; } = new();

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        Network = await ContainerHelpers.CreateNetwork("telegram-test-network");

        await Vault.InitAsync("vault", Network);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await Network.DisposeAsync();
    }
}