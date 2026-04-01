using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Minnaloushe.Core.Toolbox.StringExtensions;
using System.Diagnostics;

namespace Minnaloushe.Core.S3.S3Storage.LifecycleManagement;

internal class S3LifecycleRuleInitializationService(
    IEnumerable<IS3LifecycleRuleProcessor> lifecycleRuleProcessors,
    ILogger<S3LifecycleRuleInitializationService> logger
    )
    : BackgroundService
{
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var stopwatch = Stopwatch.StartNew();
        logger.LogStartingS3LifecycleRuleSynchronization();

        // Yield immediately to allow Kestrel to start
        await Task.Yield();

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            timeoutCts.CancelAfter(OperationTimeout);

            foreach (var synchronizer in lifecycleRuleProcessors)
            {
                try
                {
                    await synchronizer.SyncLifecycleRulesAsync();
                }
                catch (Exception ex)
                {
                    logger.LogErrorWhileSynchronizingLifecycleRules(ex, synchronizer.GetType().GetFriendlyName());
                }
            }

            stopwatch.Stop();
            logger.LogS3LifecycleRuleSynchronizationCompleted(stopwatch.Elapsed.TotalSeconds);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            logger.LogS3LifecycleRuleSynchronizationCancelled(stopwatch.Elapsed.TotalSeconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogS3LifecycleRuleSynchronizationFailed(ex, stopwatch.Elapsed.TotalSeconds);
        }
    }
}