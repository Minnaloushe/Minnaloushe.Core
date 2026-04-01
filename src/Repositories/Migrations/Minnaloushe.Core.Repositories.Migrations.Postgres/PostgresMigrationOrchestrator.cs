using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.ClientProviders.Postgres;
using Minnaloushe.Core.ReadinessProbe.Abstractions;
using Minnaloushe.Core.Repositories.Abstractions;
using Minnaloushe.Core.Repositories.Migrations.Common.HostedService;

namespace Minnaloushe.Core.Repositories.Migrations.Postgres;

/// <summary>
/// Orchestrates migrations across multiple PostgreSQL databases.
/// Groups migrations by target database and runs them using dedicated runners.
/// </summary>
internal sealed class PostgresMigrationOrchestrator(
    IEnumerable<IPostgresMigration> migrations,
    IOptionsMonitor<RepositoryOptions> optionsMonitor,
    ILogger<PostgresMigrationOrchestrator> logger,
    IReadinessProbe<PostgresMigrationOrchestrator> readinessProbe,
    IServiceProvider serviceProvider,
    ILoggerFactory loggerFactory)
    : MigrationOrchestratorBase<IPostgresMigration>(migrations, optionsMonitor, logger)
{
    protected override async Task RunRepositoryMigrationsAsync(
        RepositoryOptions options,
        List<IPostgresMigration> migrations,
        CancellationToken cancellationToken)
    {
        // Create client provider and get data source by the configured ConnectionName
        var clientProvider = serviceProvider.GetRequiredKeyedService<IPostgresClientProvider>(options.ConnectionName);
        using var clientLease = clientProvider.Acquire();

        // Create runner and execute migrations
        var runnerLogger = loggerFactory.CreateLogger<PostgresDatabaseMigrationRunner>();
        var runner = new PostgresDatabaseMigrationRunner(clientLease.Client, options.DatabaseName, runnerLogger);

        await runner.RunMigrationsAsync(
            migrations,
            lockAcquireTimeout: TimeSpan.FromSeconds(30),
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    protected override void OnAllMigrationsCompleted()
    {
        readinessProbe.SetState(HealthStatus.Healthy);
    }
}
