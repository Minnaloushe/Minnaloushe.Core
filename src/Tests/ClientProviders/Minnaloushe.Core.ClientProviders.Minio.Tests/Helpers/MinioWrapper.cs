using Microsoft.Extensions.Logging;
using Minnaloushe.Core.Toolbox.AsyncInitializer;

namespace Minnaloushe.Core.ClientProviders.Minio.Tests.Helpers;

public class MinioWrapper(IMinioClientProvider clientProvider,
    ILogger<MinioWrapper> logger
) : IMinioWrapper, IAsyncInitializer
{
    public IMinioClientProvider ClientProvider { get; } = clientProvider;
    public bool InitializationCompleted { get; private set; }

    public Task<bool> InitializeAsync(CancellationToken cancellationToken)
    {
        if (ClientProvider.Client == null!)
        {
            logger.LogWarning("Minio client is not initialized yet.");
            return Task.FromResult(false);
        }

        InitializationCompleted = true;

        return Task.FromResult(true);
    }
}
