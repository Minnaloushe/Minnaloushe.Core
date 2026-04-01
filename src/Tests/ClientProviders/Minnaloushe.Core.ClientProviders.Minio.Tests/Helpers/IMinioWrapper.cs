namespace Minnaloushe.Core.ClientProviders.Minio.Tests.Helpers;

public interface IMinioWrapper
{
    IMinioClientProvider ClientProvider { get; }
    bool InitializationCompleted { get; }
}
