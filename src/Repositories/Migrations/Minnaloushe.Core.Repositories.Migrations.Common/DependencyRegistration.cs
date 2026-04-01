using Microsoft.Extensions.DependencyInjection;
using Minnaloushe.Core.Repositories.DependencyInjection.Models;
using Minnaloushe.Core.Repositories.Migrations.Common.HostedService;

namespace Minnaloushe.Core.Repositories.Migrations.Common;

internal static class DependencyRegistration
{
    public static RepositoryBuilder AddMigrationHostedService(this RepositoryBuilder builder)
    {
        builder.Services.AddHostedService<MigrationHostedService>();

        return builder;
    }
}