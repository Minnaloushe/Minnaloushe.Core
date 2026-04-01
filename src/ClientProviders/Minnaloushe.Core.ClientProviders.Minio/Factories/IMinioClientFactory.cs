using Minio;
using Minnaloushe.Core.ClientProviders.Minio.Options;

namespace Minnaloushe.Core.ClientProviders.Minio.Factories;

public interface IMinioClientFactory
{
    IMinioClient Create(S3StorageOptions options);
}
