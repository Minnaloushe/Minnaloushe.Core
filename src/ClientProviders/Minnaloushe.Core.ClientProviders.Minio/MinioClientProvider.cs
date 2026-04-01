using Microsoft.Extensions.Logging;
using Minio;
using Minnaloushe.Core.ClientProviders.Minio.Options;
using Minnaloushe.Core.VaultOptions.ResolvedOptions;
using IMinioClientFactory = Minnaloushe.Core.ClientProviders.Minio.Factories.IMinioClientFactory;

namespace Minnaloushe.Core.ClientProviders.Minio;

public class MinioClientProvider(
    IResolvedOptions<S3StorageOptions> options,
    IMinioClientFactory factory,
    ILogger<MinioClientProvider> logger)
    : IMinioClientProvider
{
    public Task<bool> InitializeAsync(CancellationToken cancellationToken)
    {
        if (options.IsEmpty)
        {
            logger.LogWarning("S3 storage options configuration was not completed");
            return Task.FromResult(false);
        }
        if (options.Value.IsEmpty)
        {
            logger.LogWarning("S3 storage options are not properly configured.");
            return Task.FromResult(false);
        }

        Client = factory.Create(options.Value);

        return Task.FromResult(true);
    }

    public IMinioClient Client
    {
        get => field ?? throw new InvalidOperationException("Minio client has not been initialized yet.");

        private set;
    }
}
