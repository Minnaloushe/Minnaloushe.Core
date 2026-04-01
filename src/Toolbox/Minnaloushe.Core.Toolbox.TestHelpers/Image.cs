using DotNet.Testcontainers.Images;

namespace Minnaloushe.Core.Toolbox.TestHelpers;

public static class Image
{
    public static IImage FromDefaultRegistry(string image, string? registry = null)
    {
        var envRegistry = Environment.GetEnvironmentVariable("TESTCONTAINERS_DOCKER_REGISTRY").TrimEnd("/").ToString();

        var overridenRegistry = registry ?? envRegistry;

        var imageName = string.IsNullOrWhiteSpace(overridenRegistry) ? image : $"{overridenRegistry}/{image}";

        var dockerImage = new DockerImage(imageName);

        return dockerImage;
    }
}