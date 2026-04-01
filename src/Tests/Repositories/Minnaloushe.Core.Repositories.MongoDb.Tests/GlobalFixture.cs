using DotNet.Testcontainers.Networks;
using Minnaloushe.Core.Tests.Helpers;
using Minnaloushe.Core.Tests.Helpers.ContainerWrappers;

namespace Minnaloushe.Core.Repositories.MongoDb.Tests;

[SetUpFixture]
internal sealed class GlobalFixture
{
    public static INetwork Network { get; private set; } = null!;

    public static readonly MongoDbContainerWrapper MongoDb = new();

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        Network = await ContainerHelpers.CreateNetwork("mongo-repo-test");
        await MongoDb.InitAsync("mongodb", Network);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await MongoDb.DisposeAsync();
        await Network.DisposeAsync();
    }
}