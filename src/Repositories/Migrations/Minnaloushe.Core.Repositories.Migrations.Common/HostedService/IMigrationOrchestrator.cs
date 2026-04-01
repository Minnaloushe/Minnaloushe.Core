namespace Minnaloushe.Core.Repositories.Migrations.Common.HostedService;

internal interface IMigrationOrchestrator
{
    Task RunAllMigrationsAsync(CancellationToken cancellationToken);
}