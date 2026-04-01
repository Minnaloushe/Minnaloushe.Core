using Microsoft.Extensions.DependencyInjection;
using Minnaloushe.Core.ReadinessProbe;
using Minnaloushe.Core.Repositories.DependencyInjection.Models;
using Minnaloushe.Core.Repositories.Migrations.Common;
using Minnaloushe.Core.Repositories.Migrations.Common.HostedService;

namespace Minnaloushe.Core.Repositories.Migrations.MongoDb;

public static class DependencyRegistration
{
    //TODO Consider adding helper registration methods for IMongoDbMigration
    public static RepositoryBuilder AddMongoDbMigrations(this RepositoryBuilder builder)
    {
        builder.AddMigrationHostedService();

        builder.Services.AddSingletonAsReadinessProbe<MigrationOrchestrator>();

        builder.Services.AddSingleton<IMigrationOrchestrator, MigrationOrchestrator>();

        return builder;
    }

    public static IServiceCollection AddMongoDbMigration<TMigration>(this IServiceCollection services)
        where TMigration : class, IMongoDbMigration
    {
        services.AddSingleton<IMongoDbMigration, TMigration>();

        return services;
    }
}