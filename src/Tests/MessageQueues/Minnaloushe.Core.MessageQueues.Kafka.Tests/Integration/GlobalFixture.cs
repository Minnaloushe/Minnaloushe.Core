using DotNet.Testcontainers.Networks;
using Minnaloushe.Core.Tests.Helpers;
using Minnaloushe.Core.Tests.Helpers.ContainerWrappers;

namespace Minnaloushe.Core.MessageQueues.Kafka.Tests.Integration;

[SetUpFixture]
internal sealed class GlobalFixture
{
    public static string NetworkName = Helpers.UniqueString("kafka-test-network");
    public static string AppNamespace = "develop";

    public static KafkaContainerWrapper Kafka1 = new();
    public static KafkaContainerWrapper Kafka2 = new();

    public static readonly VaultContainerWrapper Vault = new();
    public static readonly ConsulContainerWrapper Consul = new();

    public static INetwork NetworkInstance = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        NetworkInstance = await ContainerHelpers.CreateNetwork(NetworkName);

        await Task.WhenAll(
            Kafka1.InitAsync("kafka-instance1", NetworkInstance),
            Kafka2.InitAsync("kafka-instance2", NetworkInstance),
            Vault.InitAsync("vault", NetworkInstance),
            Consul.InitAsync("consul", NetworkInstance)
        );

        await Consul.ConfigureRegistration(Kafka1);
        await Consul.ConfigureRegistration(Kafka2);
        await Vault.ConfigureStaticSecret($"{AppNamespace}/{Kafka1.Name}", Kafka1);
        await Vault.ConfigureStaticSecret($"{AppNamespace}/{Kafka2.Name}", Kafka2);

    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await Kafka1.DisposeAsync();
        await Kafka2.DisposeAsync();

        await NetworkInstance.DisposeAsync();
    }
}