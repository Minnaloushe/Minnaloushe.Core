using Microsoft.Extensions.DependencyInjection;
using Minnaloushe.Core.ClientProviders.Postgres;
using Minnaloushe.Core.Repositories.DependencyInjection.Extensions;
using Minnaloushe.Core.Repositories.DependencyInjection.Models;
using Minnaloushe.Core.Repositories.Postgres.Factories;
using Minnaloushe.Core.Repositories.Postgres.FactorySelectors;

namespace Minnaloushe.Core.Repositories.Postgres.Extensions;

/// <summary>
/// PostgreSQL-specific repository configuration extensions.
/// </summary>
public static class PostgresClientProviderExtensions
{
    /// <summary>
    /// Adds PostgreSQL client provider infrastructure including default factories and connection handler.
    /// Call this before Build().
    /// </summary>
    public static RepositoryBuilder AddPostgresDbClientProviders(this RepositoryBuilder builder)
    {
        builder.Services.AddSingleton<IPostgresClientProviderFactory, ConnectionStringPostgresClientProviderFactory>();
        builder.Services.AddSingleton<IPostgresClientProviderFactorySelector, PostgresClientProviderFactorySelector>();

        builder.Services.RegisterRepositoryHandler(["postgres", "postgresql"], context =>
        {
            context.RegisterKeyedProvider<
                IPostgresClientProvider,
                IPostgresClientProviderFactory,
                IPostgresClientProviderFactorySelector>();
        });

        return builder;
    }
}
