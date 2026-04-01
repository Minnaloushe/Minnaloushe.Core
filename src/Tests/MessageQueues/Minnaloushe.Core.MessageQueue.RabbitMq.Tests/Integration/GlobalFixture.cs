using DotNet.Testcontainers.Networks;
using Minnaloushe.Core.Tests.Helpers;
using Minnaloushe.Core.Tests.Helpers.ContainerWrappers;

namespace Minnaloushe.Core.MessageQueue.RabbitMq.Tests.Integration;

[SetUpFixture]
internal sealed class GlobalFixture
{
    public static INetwork Network { get; private set; } = null!;
    public static readonly VaultContainerWrapper Vault = new();
    public static readonly ConsulContainerWrapper Consul = new();

    public static readonly RabbitContainerWrapper RabbitMqInstance1 = new();
    public static readonly RabbitContainerWrapper RabbitMqInstance2 = new();
    public static string AppNamespace => "develop";

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        Network = await ContainerHelpers.CreateNetwork("rabbitmq-test-network");

        await Network.CreateAsync();

        await Task.WhenAll(
            Vault.InitAsync("vault", Network),
            Consul.InitAsync("consul", Network),
            RabbitMqInstance1.InitAsync(Helpers.UniqueString("rabbit1-"), Network),
            RabbitMqInstance2.InitAsync(Helpers.UniqueString("rabbit2-"), Network)
        );

        await Consul.ConfigureRegistration(RabbitMqInstance1);
        await Consul.ConfigureRegistration(RabbitMqInstance2);
        await Vault.ConfigureStaticSecret($"{AppNamespace}/{RabbitMqInstance1.Name}", RabbitMqInstance1);
        await Vault.ConfigureStaticSecret($"{AppNamespace}/{RabbitMqInstance2.Name}", RabbitMqInstance2);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await Consul.DisposeAsync();
        await Vault.DisposeAsync();
        await RabbitMqInstance1.DisposeAsync();
        await RabbitMqInstance2.DisposeAsync();
        await Network.DisposeAsync();
    }
}
