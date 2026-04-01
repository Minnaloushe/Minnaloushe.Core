using Microsoft.Extensions.Logging;
using Minnaloushe.Core.Logging.Redaction;

namespace Minnaloushe.Core.ClientProviders.Abstractions;

internal static partial class LeasedCredentialWatcherLogger
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "Attaching to parent credentials watcher of type {ParentType}")]
    public static partial void AttachingToParent(this ILogger logger, string parentType);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Forced refresh triggered by parent watcher failed")]
    public static partial void ForcedRefreshTriggeredByParentFailed(this ILogger logger, Exception ex);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "Attached to parent - will react to future parent credential rotations only")]
    public static partial void AttachedToParent(this ILogger logger);

    [LoggerMessage(EventId = 4, Level = LogLevel.Debug, Message = "Starting LeasedCredentialWatcher for provider {ProviderType}")]
    public static partial void StartingWatcher(this ILogger logger, string providerType);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "Initial credentials issued: LeaseId={LeaseId}, DurationSeconds={Duration}, Renewable={Renewable}")]
    public static partial void InitialCredentialsIssued(this ILogger logger, [SensitiveInformation] string leaseId, int duration, bool renewable);

    [LoggerMessage(EventId = 6, Level = LogLevel.Debug, Message = "ForceRefresh invoked")]
    public static partial void ForceRefreshInvoked(this ILogger logger);

    [LoggerMessage(EventId = 7, Level = LogLevel.Information, Message = "Credentials re-issued (forced): LeaseId={LeaseId}, DurationSeconds={Duration}, Renewable={Renewable}")]
    public static partial void CredentialsReissuedForced(this ILogger logger, [SensitiveInformation] string leaseId, int duration, bool renewable);

    [LoggerMessage(EventId = 8, Level = LogLevel.Debug, Message = "Lease duration unchanged ({DurationSeconds}s), skipping renewal loop recreation for LeaseId={LeaseId}")]
    public static partial void LeaseDurationUnchanged(this ILogger logger, int durationSeconds, [SensitiveInformation] string leaseId);

    [LoggerMessage(EventId = 9, Level = LogLevel.Debug, Message = "Starting renewal loop for LeaseId={LeaseId}. LeaseDuration={LeaseDuration}, NextRenewIn={RenewDelay}")]
    public static partial void StartingRenewalLoop(this ILogger logger, [SensitiveInformation] string leaseId, TimeSpan leaseDuration, TimeSpan renewDelay);

    [LoggerMessage(EventId = 10, Level = LogLevel.Error, Message = "Renewal loop failed")]
    public static partial void RenewalLoopFailed(this ILogger logger, Exception ex);

    [LoggerMessage(EventId = 11, Level = LogLevel.Debug, Message = "Renewal loop completed")]
    public static partial void RenewalLoopCompleted(this ILogger logger);

    [LoggerMessage(EventId = 12, Level = LogLevel.Debug, Message = "Attempting to renew or reissue credentials for LeaseId={LeaseId}. Renewable={Renewable}")]
    public static partial void AttemptingRenewOrReissue(this ILogger logger, [SensitiveInformation] string leaseId, bool renewable);

    [LoggerMessage(EventId = 13, Level = LogLevel.Trace, Message = "Credentials are renewable, attempting RenewAsync for LeaseId={LeaseId}")]
    public static partial void CredentialsAreRenewableAttemptRenew(this ILogger logger, [SensitiveInformation] string leaseId);

    [LoggerMessage(EventId = 14, Level = LogLevel.Information, Message = "Successfully renewed lease {LeaseId} - new DurationSeconds={Duration}, Renewable={Renewable}")]
    public static partial void SuccessfullyRenewedLease(this ILogger logger, [SensitiveInformation] string leaseId, int duration, bool renewable);

    [LoggerMessage(EventId = 15, Level = LogLevel.Warning, Message = "RenewAsync failed for LeaseId={LeaseId}: {Message}. Will attempt to issue new credentials")]
    public static partial void RenewAsyncFailed(this ILogger logger, Exception ex, [SensitiveInformation] string leaseId, string message);

    [LoggerMessage(EventId = 16, Level = LogLevel.Information, Message = "Issued new credentials for LeaseId={LeaseId} (replacing previous)")]
    public static partial void IssuedNewCredentials(this ILogger logger, [SensitiveInformation] string leaseId);

    [LoggerMessage(EventId = 17, Level = LogLevel.Error, Message = "Failed to renew or reissue credentials for LeaseId={LeaseId}: {Message}. Retrying by issuing new credentials")]
    public static partial void FailedToRenewOrReissue(this ILogger logger, Exception ex, [SensitiveInformation] string leaseId, string message);

    [LoggerMessage(EventId = 18, Level = LogLevel.Debug, Message = "Disposing LeasedCredentialWatcher")]
    public static partial void Disposing(this ILogger logger);

    [LoggerMessage(EventId = 19, Level = LogLevel.Information, Message = "LeasedCredentialWatcher disposed")]
    public static partial void Disposed(this ILogger logger);
}
