using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;

namespace Minnaloushe.Core.Tests.Helpers;

public sealed class ContainerHelpers
{
    public static async Task<INetwork> CreateNetwork(string name)
    {
        var network = new NetworkBuilder()
            .WithName(Helpers.UniqueString(name))
            .Build();

        await network.CreateAsync();

        return network;
    }
}