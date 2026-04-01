using Minio;
using Minnaloushe.Core.ClientProviders.Minio.Options;

namespace Minnaloushe.Core.ClientProviders.Minio.Factories;

public class MinioClientFactory : IMinioClientFactory
{
    public IMinioClient Create(S3StorageOptions options)
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = options.HttpClient.PolledConnectionLifetime,
            EnableMultipleHttp2Connections = options.HttpClient.EnableMultipleHttp2Connections,
            MaxConnectionsPerServer = options.HttpClient.MaxConnections
        };

        var httpClient = new HttpClient(handler);

        return new MinioClient()
            .WithEndpoint(options.ServiceUrl)
            .WithCredentials(options.AccessKey, options.SecretKey)
            .WithHttpClient(httpClient)
            .Build();
    }
}
