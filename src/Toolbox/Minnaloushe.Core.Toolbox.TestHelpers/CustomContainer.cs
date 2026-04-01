using DotNet.Testcontainers.Builders;

namespace Minnaloushe.Core.Toolbox.TestHelpers;

public static class CustomContainer
{
    public static ContainerBuilder FromDefaultRegistry(string image, string? registry = null)
    {
        var dockerImage = Image.FromDefaultRegistry(image, registry);

        return new ContainerBuilder(dockerImage);
    }
}