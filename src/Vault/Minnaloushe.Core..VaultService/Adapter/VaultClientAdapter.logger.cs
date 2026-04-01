using Microsoft.Extensions.Logging;
using Minnaloushe.Core.Logging.Redaction;
using Minnaloushe.Core.VaultService.Adapter;

namespace Minnaloushe.Core.VaultService.Implementations;

internal static partial class VaultClientAdapterLogger
{
    [LoggerMessage(LogLevel.Debug, "VaultClientAdapter constructed")]
    public static partial void LogVaultClientAdapterConstructed(this ILogger logger);

    [LoggerMessage(LogLevel.Debug, "Issuing Vault client credentials - starting CreateAsync")]
    public static partial void LogIssuingVaultClientCredentialsStartingCreateAsync(this ILogger<VaultClientAdapter> logger);

    [LoggerMessage(LogLevel.Debug, "Vault client instance created, performing token self-lookup")]
    public static partial void LogVaultClientInstanceCreatedPerformingTokenSelfLookup(this ILogger<VaultClientAdapter> logger);

    [LoggerMessage(LogLevel.Debug, "Vault client creation completed in {ElapsedMs}ms.")]
    public static partial void LogVaultClientCreationElapsed(this ILogger<VaultClientAdapter> logger, double ElapsedMs);

    [LoggerMessage(LogLevel.Debug, "Vault token lookup completed in {ElapsedMs}ms.")]
    public static partial void LogVaultTokenLookupElapsed(this ILogger<VaultClientAdapter> logger, double ElapsedMs);

    [LoggerMessage(LogLevel.Error, "LookupSelfAsync returned null - cannot obtain token info")]
    public static partial void LogLookupSelfAsyncReturnedNullCannotObtainTokenInfo(this ILogger<VaultClientAdapter> logger);

    [LoggerMessage(LogLevel.Information, "Issued Vault client credentials - TokenId: {TokenId}, TTL: {TTL}s, Renewable: {Renewable}")]
    public static partial void LogIssuedVaultClientCredentials(this ILogger<VaultClientAdapter> logger, [SensitiveInformation] string TokenId, int TTL, bool Renewable);

    [LoggerMessage(LogLevel.Error, "Failed to issue Vault client credentials: {Message}")]
    public static partial void LogFailedToIssueVaultClientCredentialsMessage(this ILogger<VaultClientAdapter> logger, Exception ex, string Message);

    [LoggerMessage(LogLevel.Debug, "Renewing Vault token lease: {LeaseId}")]
    public static partial void LogRenewingVaultTokenLeaseLeaseId(this ILogger<VaultClientAdapter> logger, [SensitiveInformation] string LeaseId);

    [LoggerMessage(LogLevel.Warning, "Token renew returned null for lease: {LeaseId}")]
    public static partial void LogTokenRenewReturnedNullForLeaseLeaseId(this ILogger<VaultClientAdapter> logger, [SensitiveInformation] string LeaseId);

    [LoggerMessage(LogLevel.Information, "Renewed Vault token lease {LeaseId} - new DurationSeconds: {Duration}, Renewable: {Renewable}")]
    public static partial void LogRenewedVaultTokenLease(this ILogger<VaultClientAdapter> logger, [SensitiveInformation] string LeaseId, int Duration, bool Renewable);

    [LoggerMessage(LogLevel.Error, "Failed to renew Vault token lease {LeaseId}: {Message}")]
    public static partial void LogFailedToRenewVaultTokenLease(this ILogger<VaultClientAdapter> logger, Exception ex, [SensitiveInformation] string LeaseId, string Message);

    [LoggerMessage(LogLevel.Debug, "Initializing VaultClientAdapter watcher")]
    public static partial void LogInitializingVaultClientAdapterWatcher(this ILogger<VaultClientAdapter> logger);

    [LoggerMessage(LogLevel.Information, "VaultClientAdapter watcher started")]
    public static partial void LogVaultClientAdapterWatcherStarted(this ILogger<VaultClientAdapter> logger);

    [LoggerMessage(LogLevel.Debug, "Disposing VaultClientAdapter watcher")]
    public static partial void LogDisposingVaultClientAdapterWatcher(this ILogger<VaultClientAdapter> logger);

    [LoggerMessage(LogLevel.Information, "VaultClientAdapter disposed")]
    public static partial void LogVaultClientAdapterDisposed(this ILogger<VaultClientAdapter> logger);

    [LoggerMessage(LogLevel.Trace, "Acquiring client lease from holder")]
    public static partial void LogAcquiringClientLeaseFromHolder(this ILogger<VaultClientAdapter> logger);

    [LoggerMessage(LogLevel.Debug, "Attaching VaultClientAdapter watcher to parent watcher of type {ParentType}")]
    public static partial void LogAttachingVaultClientAdapterWatcherToParentWatcher(this ILogger<VaultClientAdapter> logger, string ParentType);
}