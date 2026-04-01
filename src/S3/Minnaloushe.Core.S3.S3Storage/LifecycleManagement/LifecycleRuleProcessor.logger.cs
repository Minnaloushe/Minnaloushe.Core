using Microsoft.Extensions.Logging;

namespace Minnaloushe.Core.S3.S3Storage.LifecycleManagement;

internal static partial class S3LifecycleRuleSynchronizerLogger
{
    [LoggerMessage(LogLevel.Information, "S3 lifecycle rule synchronization is disabled in configuration; skipping.")]
    public static partial void LogS3LifecycleRuleSynchronizationDisabledInConfigurationSkipping(this ILogger<LifecycleRuleProcessor> logger);

    [LoggerMessage(LogLevel.Warning, "Lifecycle rule sync failed. Bucket {BucketName} does not exist")]
    public static partial void LogLifecycleRuleSyncBucketDoesNotExist(this ILogger<LifecycleRuleProcessor> logger, string bucketName);

    [LoggerMessage(LogLevel.Information, "Synchronized lifecycle rules for {BucketName}")]
    public static partial void LogSynchronizedLifecycleRulesForBucket(this ILogger<LifecycleRuleProcessor> logger, string bucketName);

    [LoggerMessage(LogLevel.Information, "Lifecycle rules for {BucketName} are already up-to-date")]
    public static partial void LogLifecycleRulesForBucketAlreadyUpToDate(this ILogger<LifecycleRuleProcessor> logger, string bucketName);

    [LoggerMessage(LogLevel.Error, "MinIO error syncing lifecycle rules for bucket {BucketName}")]
    public static partial void LogMinioErrorSyncingLifecycleRulesForBucket(this ILogger<LifecycleRuleProcessor> logger, Exception ex, string bucketName);

    [LoggerMessage(LogLevel.Error, "Unexpected error syncing lifecycle rules for bucket {BucketName}")]
    public static partial void LogUnexpectedErrorSyncingLifecycleRulesForBucket(this ILogger<LifecycleRuleProcessor> logger, Exception ex, string bucketName);
}
