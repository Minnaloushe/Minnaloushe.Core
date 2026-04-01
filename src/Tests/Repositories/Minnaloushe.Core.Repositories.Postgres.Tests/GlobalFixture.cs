using DotNet.Testcontainers.Networks;
using Minnaloushe.Core.Tests.Helpers;
using Minnaloushe.Core.Tests.Helpers.ContainerWrappers;

namespace Minnaloushe.Core.Repositories.Postgres.Tests;

[SetUpFixture]
internal sealed class GlobalFixture
{
    public static INetwork Network { get; private set; } = null!;

    public static readonly PostgresContainerWrapper Postgres = new();

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        Network = await ContainerHelpers.CreateNetwork("postgres-migration-test");
        await Postgres.InitAsync("postgres", Network);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await Postgres.DisposeAsync();
        await Network.DisposeAsync();
    }
}
