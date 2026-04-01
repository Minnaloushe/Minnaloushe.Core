using Microsoft.Extensions.DependencyInjection;
using Minnaloushe.Core.ReadinessProbe;
using Minnaloushe.Core.Repositories.DependencyInjection.Models;
using Minnaloushe.Core.Repositories.Migrations.Common;
using Minnaloushe.Core.Repositories.Migrations.Common.HostedService;

namespace Minnaloushe.Core.Repositories.Migrations.Postgres;

//TODO Borrow FluentMigrator fluent syntax
public static class DependencyRegistration
{
    public static RepositoryBuilder AddPostgresMigrations(this RepositoryBuilder builder)
    {
        builder.AddMigrationHostedService();

        builder.Services.AddSingletonAsReadinessProbe<PostgresMigrationOrchestrator>();

        builder.Services.AddSingleton<IMigrationOrchestrator, PostgresMigrationOrchestrator>();

        return builder;
    }

    public static IServiceCollection AddPostgresMigration<TMigration>(this IServiceCollection services)
        where TMigration : class, IPostgresMigration
    {
        services.AddSingleton<IPostgresMigration, TMigration>();

        return services;
    }
}
