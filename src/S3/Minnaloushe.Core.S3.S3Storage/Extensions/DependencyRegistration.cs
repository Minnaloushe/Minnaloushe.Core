using Microsoft.Extensions.DependencyInjection;
using Minnaloushe.Core.ClientProviders.Abstractions.VaultExtensions;
using Minnaloushe.Core.ClientProviders.Minio;
using Minnaloushe.Core.ClientProviders.Minio.Options;
using Minnaloushe.Core.S3.S3Storage.Adapter;
using Minnaloushe.Core.S3.S3Storage.CompressedStorageAdapter;
using Minnaloushe.Core.S3.S3Storage.LifecycleManagement;
using Minnaloushe.Core.S3.S3Storage.MetadataAdapter;
using Minnaloushe.Core.Toolbox.RecyclableMemoryStream.Extensions;
using Minnaloushe.Core.VaultOptions.ResolvedOptions;

namespace Minnaloushe.Core.S3.S3Storage.Extensions;

public static class DependencyRegistration
{
    public static IServiceCollection ConfigureS3Storage(this IServiceCollection services)
    {
        services.AddHostedService<S3LifecycleRuleInitializationService>();

        return services;
    }
    public static IServiceCollection AddS3StorageAdapter(this IServiceCollection services)
    {
        services.AddSingleton<IS3StorageAdapter, S3StorageAdapter>();
        services.AddSingleton<IS3CompressedStorageAdapter, S3CompressedStorageAdapter>();
        services.AddSingleton<IS3LifecycleRuleProcessor, LifecycleRuleProcessor>();
        services.AddSingleton<IS3MetadataAdapter, S3MetadataAdapter>();

        services.ConfigureRecyclableStreams();

        return services;
    }

    public static IServiceCollection AddKeyedS3StorageAdapters(this IServiceCollection services, object? key)
    {
        services.AddKeyedSingleton<IS3StorageAdapter, S3StorageAdapter>(key, (sp, k) =>
            {
                var options = sp.GetRequiredKeyedService<S3StorageOptions>(k);
                var provider = sp.GetRequiredKeyedService<IMinioClientProvider>(k);
                return ActivatorUtilities.CreateInstance<S3StorageAdapter>(sp, options, provider);
            }
        );

        services.AddKeyedSingleton<IS3MetadataAdapter, S3MetadataAdapter>(key, (sp, k) =>
            {
                var adapter = sp.GetRequiredKeyedService<IS3StorageAdapter>(k);
                return ActivatorUtilities.CreateInstance<S3MetadataAdapter>(sp, adapter);
            }
        );

        services.AddKeyedSingleton<IS3CompressedStorageAdapter, S3CompressedStorageAdapter>(key, (sp, k) =>
        {
            var adapter = sp.GetRequiredKeyedService<IS3StorageAdapter>(k);
            return ActivatorUtilities.CreateInstance<S3CompressedStorageAdapter>(sp, adapter);
        });

        services.AddKeyedSingleton<IS3LifecycleRuleProcessor>(key, (sp, k) =>
        {
            var options = sp.GetRequiredKeyedService<S3StorageOptions>(k);
            var provider = sp.GetRequiredKeyedService<IMinioClientProvider>(k);
            return ActivatorUtilities.CreateInstance<LifecycleRuleProcessor>(sp, options, provider);
        });

        return services;
    }

    public static KeyedSingletonBuilder WithStorageAdapters(this KeyedSingletonBuilder builder)
    {
        builder.Services.ConfigureRecyclableStreams();

        foreach (var key in builder.Keys)
        {
            builder.Services.AddKeyedSingleton(key, (sp, k) =>
            {
                var resolvedOptions = sp.GetRequiredService<IResolvedKeyedOptions<S3StorageOptions>>();
                return resolvedOptions.Get((string)k)?.Value
                    ?? throw new InvalidOperationException($"S3StorageOptions for key '{k}' have not been initialized.");
            });

            builder.Services.AddKeyedS3StorageAdapters(key);
        }

        return builder;
    }
}

