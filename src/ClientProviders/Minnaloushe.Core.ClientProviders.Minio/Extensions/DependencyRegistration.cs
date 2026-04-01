using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Minnaloushe.Core.ClientProviders.Abstractions.VaultExtensions;
using Minnaloushe.Core.ClientProviders.Minio.Factories;
using Minnaloushe.Core.ClientProviders.Minio.Options;

namespace Minnaloushe.Core.ClientProviders.Minio.Extensions;

public static class DependencyRegistration
{
    public static KeyedSingletonBuilder AddKeyedMinioClientProviders(this IServiceCollection services,
        IConfiguration configuration)
    {
        return services.RegisterKeyedClientProvider<
            S3StorageOptions,
            IMinioClientProvider,
            MinioClientProvider,
            IMinioClientFactory,
            MinioClientFactory>(
            configuration,
            sectionName: S3StorageOptions.SectionName,
            providerFactory: (sp, _, factory, resolvedOptions) =>
                new MinioClientProvider(
                    resolvedOptions,
                    factory,
                    sp.GetRequiredService<ILogger<MinioClientProvider>>()
                    )
            );
    }

    public static IServiceCollection AddMinioClientProvider(this IServiceCollection services)
    {
        return services.AddClientProvider<
            IMinioClientProvider,
            MinioClientProvider,
            S3StorageOptions>(S3StorageOptions.SectionName);
    }
}