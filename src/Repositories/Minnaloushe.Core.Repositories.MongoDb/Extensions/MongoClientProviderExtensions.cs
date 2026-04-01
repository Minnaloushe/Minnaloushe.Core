using Microsoft.Extensions.DependencyInjection;
using Minnaloushe.Core.ClientProviders.MongoDb;
using Minnaloushe.Core.Repositories.DependencyInjection.Extensions;
using Minnaloushe.Core.Repositories.DependencyInjection.Models;
using Minnaloushe.Core.Repositories.MongoDb.Factories;
using Minnaloushe.Core.Repositories.MongoDb.FactorySelectors;

namespace Minnaloushe.Core.Repositories.MongoDb.Extensions;

/// <summary>
/// MongoDB-specific repository configuration extensions.
/// </summary>
public static class MongoClientProviderExtensions
{
    /// <summary>
    /// Adds MongoDB client provider infrastructure including default factories and connection handler.
    /// Call this before Build().
    /// </summary>
    public static RepositoryBuilder AddMongoDbClientProviders(this RepositoryBuilder builder, Action? customMappings = null)
    {
        ConfigureMappings(customMappings);

        builder.Services.AddSingleton<IMongoClientProviderFactory, ConnectionStringMongoClientProviderFactory>();
        builder.Services.AddSingleton<IMongoClientProviderFactorySelector, MongoClientProviderFactorySelector>();

        builder.Services.RegisterRepositoryHandler("mongodb", context =>
        {
            context.RegisterKeyedProvider<
                IMongoClientProvider,
                IMongoClientProviderFactory,
                IMongoClientProviderFactorySelector>();
        });

        return builder;
    }

    private static void ConfigureMappings(Action? customMappings)
    {
        customMappings?.Invoke();
    }
}
