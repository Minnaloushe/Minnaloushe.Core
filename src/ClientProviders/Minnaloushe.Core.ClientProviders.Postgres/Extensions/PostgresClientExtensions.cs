using Microsoft.Extensions.DependencyInjection;
using Minnaloushe.Core.ClientProviders.Abstractions.CredentialsWatcher;
using Minnaloushe.Core.ClientProviders.Postgres.Factories;
using Npgsql;

namespace Minnaloushe.Core.ClientProviders.Postgres.Extensions;

public static class PostgresClientExtensions
{
    public static IServiceCollection AddPostgresRegistration(this IServiceCollection services)
    {
        services.AddSingleton<IPostgresClientFactory, PostgresClientFactory>();
        services.AddSingleton<IRenewableClientHolder<NpgsqlDataSource>, RenewableClientHolder<NpgsqlDataSource>>();

        return services;
    }
}
