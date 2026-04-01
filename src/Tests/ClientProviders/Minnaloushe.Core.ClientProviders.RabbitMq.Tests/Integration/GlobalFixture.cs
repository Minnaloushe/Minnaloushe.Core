using DotNet.Testcontainers.Networks;
using Minnaloushe.Core.Tests.Helpers;
using Minnaloushe.Core.Tests.Helpers.ContainerWrappers;

namespace Minnaloushe.Core.ClientProviders.RabbitMq.Tests.Integration;

[SetUpFixture]
internal sealed class GlobalFixture
{
    #region Fields/Members/Constants

    public static INetwork Network { get; private set; } = null!;
    public static readonly VaultContainerWrapper Vault = new();
    public static readonly RabbitContainerWrapper RabbitMqInstance1 = new();
    public static readonly RabbitContainerWrapper RabbitMqInstance2 = new();
    public static string AppNamespace => "develop";

    #endregion

    #region Setup/Teardown

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        Network = await ContainerHelpers.CreateNetwork("rabbitmq-client-provider-test-network");

        await Task.WhenAll(
            Vault.InitAsync("vault", Network),
            RabbitMqInstance1.InitAsync(Helpers.UniqueString("rabbit1"), Network),
            RabbitMqInstance2.InitAsync(Helpers.UniqueString("rabbit2"), Network)
        );

        await Vault.ConfigureStaticSecret($"{AppNamespace}/{RabbitMqInstance1.Name}", RabbitMqInstance1);
        await Vault.ConfigureStaticSecret($"{AppNamespace}/{RabbitMqInstance2.Name}", RabbitMqInstance2);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await Vault.DisposeAsync();
        await RabbitMqInstance1.DisposeAsync();
        await RabbitMqInstance2.DisposeAsync();
        await Network.DisposeAsync();
    }

    #endregion
}