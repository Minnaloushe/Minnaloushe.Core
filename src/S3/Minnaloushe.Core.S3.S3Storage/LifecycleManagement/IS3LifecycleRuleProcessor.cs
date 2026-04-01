namespace Minnaloushe.Core.S3.S3Storage.LifecycleManagement;

internal interface IS3LifecycleRuleProcessor
{
    Task SyncLifecycleRulesAsync();
}