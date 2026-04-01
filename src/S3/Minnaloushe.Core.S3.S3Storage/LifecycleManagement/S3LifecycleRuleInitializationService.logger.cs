using Microsoft.Extensions.Logging;

namespace Minnaloushe.Core.S3.S3Storage.LifecycleManagement;

internal static partial class S3LifecycleRuleInitializationServiceLogger
{
    [LoggerMessage(LogLevel.Information, "Starting S3 lifecycle rule synchronization")]
    public static partial void LogStartingS3LifecycleRuleSynchronization(this ILogger<S3LifecycleRuleInitializationService> logger);

    [LoggerMessage(LogLevel.Information,
        "S3 lifecycle rule synchronization completed successfully in {elapsedSeconds:F4}s")]
    public static partial void LogS3LifecycleRuleSynchronizationCompleted(
        this ILogger<S3LifecycleRuleInitializationService> logger,
        double elapsedSeconds);

    [LoggerMessage(LogLevel.Warning, "S3 lifecycle rule synchronization cancelled after {elapsedSeconds:F4}s")]
    public static partial void LogS3LifecycleRuleSynchronizationCancelled(
        this ILogger<S3LifecycleRuleInitializationService> logger,
        double elapsedSeconds);

    [LoggerMessage(LogLevel.Error, "S3 lifecycle rule synchronization failed after {elapsedSeconds:F4}s. " +
                                   "Application will continue but lifecycle rules may not be synchronized.")]
    public static partial void LogS3LifecycleRuleSynchronizationFailed(
        this ILogger<S3LifecycleRuleInitializationService> logger,
        Exception ex,
        double elapsedSeconds);
    [LoggerMessage(LogLevel.Error, "An error occurred while synchronizing lifecycle rules with {SynchronizerType}")]
    public static partial void LogErrorWhileSynchronizingLifecycleRules(
        this ILogger<S3LifecycleRuleInitializationService> logger,
        Exception ex,
        string SynchronizerType);
}