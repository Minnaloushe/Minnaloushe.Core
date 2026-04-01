using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.Repositories.Abstractions;
using System.Diagnostics;

namespace Minnaloushe.Core.Repositories.Migrations.Common.HostedService;

/// <summary>
/// Base orchestrator that handles grouping migrations by target repository,
/// resolving repository options, and delegating execution to database-specific runners.
/// </summary>
internal abstract class MigrationOrchestratorBase<TMigration>(
    IEnumerable<TMigration> migrations,
    IOptionsMonitor<RepositoryOptions> optionsMonitor,
    ILogger logger)
    : IMigrationOrchestrator
    where TMigration : IMigration
{
    protected IOptionsMonitor<RepositoryOptions> OptionsMonitor => optionsMonitor;

    /// <summary>
    /// Runs all registered migrations across all configured databases.
    /// </summary>
    public async Task RunAllMigrationsAsync(CancellationToken cancellationToken = default)
    {
        var overallStopwatch = Stopwatch.StartNew();

        // Group by the repository name declared by each migration (TargetRepository)
        var migrationsByRepository = migrations
            .GroupBy(k => k.TargetRepository, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (migrationsByRepository.Count == 0)
        {
            logger.NoMigrationsRegistered();
            OnAllMigrationsCompleted();
            return;
        }

        logger.StartingMigrations(migrationsByRepository.Count);

        foreach (var repoGroup in migrationsByRepository)
        {
            var repositoryStopwatch = Stopwatch.StartNew();
            var repositoryName = repoGroup.Key;
            var migrationsList = repoGroup.ToList();

            logger.ProcessingMigrations(migrationsList.Count, repositoryName);

            // Get repository options for this repository name (named options)
            var options = GetOptionsForRepository(repositoryName);
            if (options == null)
            {
                logger.NoConfigurationFound(repositoryName);
                continue;
            }

            if (!options.Migrations.Enabled)
            {
                repositoryStopwatch.Stop();
                logger.MigrationsDisabled(repositoryName, repositoryStopwatch.ElapsedMilliseconds);
                continue;
            }

            await RunRepositoryMigrationsAsync(options, migrationsList, cancellationToken)
                .ConfigureAwait(false);

            repositoryStopwatch.Stop();
            logger.RepositoryMigrationsCompleted(repositoryName, repositoryStopwatch.Elapsed.TotalSeconds);
        }

        overallStopwatch.Stop();
        logger.AllMigrationsCompleted(overallStopwatch.Elapsed.TotalSeconds);

        OnAllMigrationsCompleted();
    }

    /// <summary>
    /// Runs migrations for a specific repository using a database-specific runner.
    /// </summary>
    protected abstract Task RunRepositoryMigrationsAsync(
        RepositoryOptions options,
        List<TMigration> migrations,
        CancellationToken cancellationToken);

    /// <summary>
    /// Called after all migrations have completed successfully.
    /// </summary>
    protected virtual void OnAllMigrationsCompleted() { }

    /// <summary>
    /// Resolves <see cref="RepositoryOptions"/> for the given repository name.
    /// Override to add database-specific fallback resolution logic.
    /// </summary>
    protected virtual RepositoryOptions? GetOptionsForRepository(string repositoryName)
    {
        try
        {
            return optionsMonitor.Get(repositoryName);
        }
        catch
        {
            return null;
        }
    }
}
