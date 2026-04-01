using DotNet.Testcontainers.Networks;
using Minnaloushe.Core.Tests.Helpers;
using Minnaloushe.Core.Tests.Helpers.ContainerWrappers;

namespace Minnaloushe.Core.ClientProviders.MongoDb.Tests.Integration;

[SetUpFixture]
internal sealed class GlobalFixture
{
    public static INetwork Network { get; private set; } = null!;

    public static readonly VaultContainerWrapper Vault = new();
    public static readonly ConsulContainerWrapper Consul = new();

    public static readonly MongoDbContainerWrapper MongoDbInstance1 = new();
    public static readonly MongoDbContainerWrapper MongoDbInstance2 = new();

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        Network = await ContainerHelpers.CreateNetwork("mongo-vault-test");

        await Task.WhenAll(
            Vault.InitAsync("vault", Network),
            Consul.InitAsync("consul", Network),
            MongoDbInstance1.InitAsync("mongodb-1", Network),
            MongoDbInstance2.InitAsync("mongodb-2", Network)
        );

        await Vault.ConfigureVaultMongoConnection(MongoDbInstance1);
        await Vault.ConfigureVaultMongoConnection(MongoDbInstance2);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await MongoDbInstance1.DisposeAsync();
        await MongoDbInstance2.DisposeAsync();

        await Vault.DisposeAsync();
        await Consul.DisposeAsync();

        await Network.DisposeAsync();
    }
}