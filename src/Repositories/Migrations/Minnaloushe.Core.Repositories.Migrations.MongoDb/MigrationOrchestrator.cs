using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.ClientProviders.MongoDb;
using Minnaloushe.Core.ReadinessProbe.Abstractions;
using Minnaloushe.Core.Repositories.Abstractions;
using Minnaloushe.Core.Repositories.Migrations.Common.HostedService;

namespace Minnaloushe.Core.Repositories.Migrations.MongoDb;

/// <summary>
/// Orchestrates migrations across multiple MongoDB databases.
/// Groups migrations by target database and runs them using dedicated runners.
/// </summary>
internal sealed class MigrationOrchestrator(
    IEnumerable<IMongoDbMigration> migrations,
    IOptionsMonitor<RepositoryOptions> optionsMonitor,
    ILogger<MigrationOrchestrator> logger,
    IReadinessProbe<MigrationOrchestrator> readinessProbe,
    IServiceProvider serviceProvider,
    ILoggerFactory loggerFactory)
    : MigrationOrchestratorBase<IMongoDbMigration>(migrations, optionsMonitor, logger)
{
    protected override async Task RunRepositoryMigrationsAsync(
        RepositoryOptions options,
        List<IMongoDbMigration> migrations,
        CancellationToken cancellationToken)
    {
        // Create client provider and get database by the configured DatabaseName
        var clientProvider = serviceProvider.GetRequiredKeyedService<IMongoClientProvider>(options.ConnectionName);
        using var clientLease = clientProvider.Acquire();
        var database = clientLease.Client.GetDatabase(options.DatabaseName);

        // Create runner and execute migrations
        var runnerLogger = loggerFactory.CreateLogger<MongoDbDatabaseMigrationRunner>();
        var runner = new MongoDbDatabaseMigrationRunner(database, runnerLogger);

        await runner.RunMigrationsAsync(
            migrations,
            lockAcquireTimeout: TimeSpan.FromSeconds(30),
            lockStaleTimeout: TimeSpan.FromMinutes(5),
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    protected override void OnAllMigrationsCompleted()
    {
        readinessProbe.SetState(HealthStatus.Healthy);
    }

    protected override RepositoryOptions? GetOptionsForRepository(string repositoryName)
    {
        var options = base.GetOptionsForRepository(repositoryName);
        if (options != null)
        {
            return options;
        }

        // Fallback: try common repository names or try to find an option whose DatabaseName matches the repositoryName.
        var commonNames = new[] { "InpxFilesRepository", "ProcessedFilesRepository" };
        foreach (var name in commonNames)
        {
            try
            {
                var fallbackOptions = OptionsMonitor.Get(name);
                if (fallbackOptions.DatabaseName.Equals(repositoryName, StringComparison.OrdinalIgnoreCase))
                {
                    return fallbackOptions;
                }
            }
            catch
            {
                // Continue to next
            }
        }

        return null;
    }
}