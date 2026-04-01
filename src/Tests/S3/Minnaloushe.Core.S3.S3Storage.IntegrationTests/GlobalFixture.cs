using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Minnaloushe.Core.Toolbox.TestHelpers;

namespace Minnaloushe.Core.S3.S3Storage.IntegrationTests;

[SetUpFixture]
public sealed class GlobalFixture
{
    public const string MinioImage = "minio/minio:RELEASE.2025-09-07T16-13-09Z";

    public static IContainer MinioInstance { get; private set; } = null!;
    public static string Endpoint { get; private set; } = null!;

    public const string MinioUser = "minioadmin";
    public const string MinioPassword = "minioadmin";
    public const int MinioPort = 9000;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        MinioInstance = new ContainerBuilder(Image.FromDefaultRegistry(MinioImage))
            .WithPortBinding(MinioPort, true)
            .WithEnvironment("MINIO_ROOT_USER", MinioUser)
            .WithEnvironment("MINIO_ROOT_PASSWORD", MinioPassword)
            .WithCommand("server", "/data")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(
                    request => request.ForPath("/minio/health/live").ForPort(MinioPort).WithMethod(System.Net.Http.HttpMethod.Get)
                ))
            .Build();

        await MinioInstance.StartAsync();

        var port = MinioInstance.GetMappedPublicPort(MinioPort);
        Endpoint = $"http://localhost:{port}";
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await MinioInstance.DisposeAsync();
    }
}
