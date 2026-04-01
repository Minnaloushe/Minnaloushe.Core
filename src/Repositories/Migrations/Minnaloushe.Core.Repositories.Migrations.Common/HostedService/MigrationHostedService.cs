using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Minnaloushe.Core.Repositories.Migrations.Common.HostedService;

/// <summary>
/// Hosted service that runs database migrations during application startup.
/// The application will not start until migrations complete successfully.
/// By the time when hosted service is started, client providers will be initialized through IAsyncInitializer,
/// so migrations can safely resolve and use repository clients.
/// </summary>
internal sealed class MigrationHostedService(
    IEnumerable<IMigrationOrchestrator> orchestrators,
    ILogger<MigrationHostedService> logger,
    IHostApplicationLifetime applicationLifetime)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        logger.StartingMigrations();

        try
        {
            foreach (var orchestratorInstance in orchestrators)
            {
                await orchestratorInstance.RunAllMigrationsAsync(cancellationToken).ConfigureAwait(false);
            }

            stopwatch.Stop();

            logger.MigrationsCompleted(stopwatch.Elapsed.TotalSeconds);
        }
        catch (OperationCanceledException ex)
        {
            stopwatch.Stop();
            logger.MigrationsCancelled(ex, stopwatch.Elapsed.TotalSeconds);
            applicationLifetime.StopApplication();
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.MigrationsFailed(ex, stopwatch.Elapsed.TotalSeconds);
            applicationLifetime.StopApplication();

            throw new InvalidOperationException(
                "Application startup failed due to database migration errors. Check logs for details.",
                ex);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.ServiceStopping();
        return Task.CompletedTask;
    }
}

