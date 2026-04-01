using DotNet.Testcontainers.Networks;
using Minnaloushe.Core.Tests.Helpers;
using Minnaloushe.Core.Tests.Helpers.ContainerWrappers;

namespace Minnaloushe.Core.ClientProviders.Postgre.Tests.Integration;

[SetUpFixture]
public class GlobalFixture
{
    public static INetwork Network { get; private set; } = null!;

    public static VaultContainerWrapper Vault { get; private set; } = new();
    public static ConsulContainerWrapper Consul { get; private set; } = new();

    public static PostgresContainerWrapper Postgres1 { get; private set; } = new();
    public static PostgresContainerWrapper Postgres2 { get; private set; } = new();


    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        Network = await ContainerHelpers.CreateNetwork("postgres-vault-test");

        await Task.WhenAll(
            Vault.InitAsync("vault", Network),
            Consul.InitAsync("consul", Network),
            Postgres1.InitAsync("postgres-1", Network),
            Postgres2.InitAsync("postgres-2", Network)
        );

        await Vault.ConfigureVaultPostgresConnection(Postgres1);
        await Vault.ConfigureVaultPostgresConnection(Postgres2);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await Postgres1.DisposeAsync();
        await Postgres2.DisposeAsync();

        await Consul.DisposeAsync();
        await Vault.DisposeAsync();

        await Network.DisposeAsync();
    }
}