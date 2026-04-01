using Microsoft.Extensions.Logging;
using Minio.DataModel.Args;
using Minio.DataModel.ILM;
using Minio.Exceptions;
using Minnaloushe.Core.ClientProviders.Minio;
using Minnaloushe.Core.ClientProviders.Minio.Options;
using Minnaloushe.Core.S3.S3Storage.Exceptions;
using Minnaloushe.Core.Toolbox.Cancellation;
using System.Collections.ObjectModel;
using LifecycleRule = Minio.DataModel.ILM.LifecycleRule;

namespace Minnaloushe.Core.S3.S3Storage.LifecycleManagement;

internal class LifecycleRuleProcessor(
    IMinioClientProvider minioClient,
    S3StorageOptions options,
    ILogger<LifecycleRuleProcessor> logger)
    : IS3LifecycleRuleProcessor
{
    public async Task SyncLifecycleRulesAsync()
    {
        if (!options.SyncRules)
        {
            logger.LogS3LifecycleRuleSynchronizationDisabledInConfigurationSkipping();
            return;
        }

        try
        {
            if (!await minioClient.Client.BucketExistsAsync(
                    new BucketExistsArgs()
                        .WithBucket(options.BucketName),
                    CancellationContext.Current))
            {
                logger.LogLifecycleRuleSyncBucketDoesNotExist(options.BucketName);
                return;
            }

            // Get current lifecycle configuration using MinIO
            LifecycleConfiguration currentConfig;
            try
            {
                var getBucketLifecycleArgs = new GetBucketLifecycleArgs().WithBucket(options.BucketName);
                currentConfig = await minioClient.Client.GetBucketLifecycleAsync(getBucketLifecycleArgs, CancellationContext.Current);
            }
            catch (MinioException ex) when (ex.Message.Contains("NoSuchLifecycleConfiguration") || ex.Message.Contains("does not exist"))
            {
                currentConfig = new LifecycleConfiguration { Rules = [] };
            }

            // Convert desired rules to MinIO format
            var desiredRules = options.LifecycleRules.Select(r => new LifecycleRule
            {
                ID = $"expire-{r.Prefix.Replace("/", "-")}",
                Status = "Enabled",
                Filter = new RuleFilter
                {
                    Prefix = r.Prefix
                },
                Expiration = new Expiration
                {
                    Days = r.ExpirationInDays
                }
            }).ToList();

            // Helper to get prefix from rule
            static string GetPrefix(LifecycleRule rule)
            {
                return rule.Filter?.Prefix ?? string.Empty;
            }

            // Compare current and desired rules
            var currentRules = currentConfig.Rules?.ToList() ?? [];
            var rulesMatch = currentRules.Count == desiredRules.Count &&
                             currentRules.All(cr => desiredRules.Any(dr =>
                                 dr.ID == cr.ID &&
                                 dr.Status == cr.Status &&
                                 GetPrefix(dr) == GetPrefix(cr) &&
                                 (int)(dr.Expiration?.Days ?? 0) == (int)(cr.Expiration?.Days ?? 0)));

            if (!rulesMatch)
            {
                // Update lifecycle configuration using MinIO
                var newLifecycleConfig = new LifecycleConfiguration
                {
                    Rules = new Collection<LifecycleRule>(desiredRules)
                };

                if (desiredRules.Count > 0)
                {
                    var setBucketLifecycleArgs = new SetBucketLifecycleArgs()
                        .WithBucket(options.BucketName)
                        .WithLifecycleConfiguration(newLifecycleConfig);

                    await minioClient.Client.SetBucketLifecycleAsync(setBucketLifecycleArgs, CancellationContext.Current);
                }
                else
                {
                    var removeBucketLifecycleArgs = new RemoveBucketLifecycleArgs()
                        .WithBucket(options.BucketName);
                    await minioClient.Client.RemoveBucketLifecycleAsync(removeBucketLifecycleArgs,
                        CancellationContext.Current);
                }

                logger.LogSynchronizedLifecycleRulesForBucket(options.BucketName);
            }
            else
            {
                logger.LogLifecycleRulesForBucketAlreadyUpToDate(options.BucketName);
            }
        }
        catch (MinioException ex)
        {
            logger.LogMinioErrorSyncingLifecycleRulesForBucket(ex, options.BucketName);
            throw new S3StorageRuleSynchronizationException(
                $"Failed to sync lifecycle rules for bucket '{options.BucketName}': {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            logger.LogUnexpectedErrorSyncingLifecycleRulesForBucket(ex, options.BucketName);
            throw new S3StorageRuleSynchronizationException(
                $"Unexpected error syncing lifecycle rules for bucket '{options.BucketName}': {ex.Message}", ex);
        }
    }
}