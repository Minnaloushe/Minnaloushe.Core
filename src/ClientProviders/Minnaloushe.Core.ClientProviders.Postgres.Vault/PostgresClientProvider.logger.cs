using Microsoft.Extensions.Logging;
using Minnaloushe.Core.Logging.Redaction;

namespace Minnaloushe.Core.ClientProviders.Postgres.Vault;

public static partial class PostgresClientProviderLogger
{
    [LoggerMessage(EventId = 3000, Level = LogLevel.Debug, Message = "PostgresClientProvider constructed for connection '{ConnectionName}'")]
    public static partial void Constructed(this ILogger logger, string ConnectionName);

    [LoggerMessage(EventId = 3001, Level = LogLevel.Trace, Message = "Acquiring NpgsqlConnection lease for connection '{ConnectionName}'")]
    public static partial void AcquiringLease(this ILogger logger, string ConnectionName);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Debug, Message = "Issuing PostgreSQL credentials for connection '{ConnectionName}'")]
    public static partial void IssuingCredentials(this ILogger logger, string ConnectionName);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Trace, Message = "Acquired Vault client lease to request DB credentials for connection '{ConnectionName}'")]
    public static partial void AcquiredVaultClientLease(this ILogger logger, string ConnectionName);

    [LoggerMessage(EventId = 3004, Level = LogLevel.Debug, Message = "Repository options for '{ConnectionName}': ServiceName={ServiceName}, DatabaseName={DatabaseName}")]
    public static partial void RepositoryOptions(this ILogger logger, string ConnectionName, string ServiceName, string DatabaseName);

    [LoggerMessage(EventId = 3005, Level = LogLevel.Debug, Message = "Resolved database role for service '{ServiceName}': {Role}")]
    public static partial void ResolvedDatabaseRole(this ILogger logger, string ServiceName, string Role);

    [LoggerMessage(EventId = 3006, Level = LogLevel.Information, Message = "Received PostgreSQL credentials lease: LeaseId={LeaseId}, DurationSeconds={Duration}, Renewable={Renewable} for connection '{ConnectionName}'")]
    public static partial void ReceivedCredentialsLease(this ILogger logger, [SensitiveInformation] string LeaseId, int Duration, bool Renewable, string ConnectionName);

    [LoggerMessage(EventId = 3007, Level = LogLevel.Information, Message = "Rotated NpgsqlConnection for connection '{ConnectionName}' (Service={ServiceName}, Database={DatabaseName})")]
    public static partial void RotatedClient(this ILogger logger, string ConnectionName, string ServiceName, string DatabaseName);

    [LoggerMessage(EventId = 3008, Level = LogLevel.Error, Message = "Failed to issue PostgreSQL credentials for connection '{ConnectionName}': {Message}")]
    public static partial void IssueFailed(this ILogger logger, Exception exception, string ConnectionName, string Message);

    [LoggerMessage(EventId = 3009, Level = LogLevel.Debug, Message = "Renewing PostgreSQL credentials lease '{LeaseId}' for connection '{ConnectionName}'")]
    public static partial void RenewingLease(this ILogger logger, [SensitiveInformation] string LeaseId, string ConnectionName);

    [LoggerMessage(EventId = 3010, Level = LogLevel.Trace, Message = "Requesting lease renewal for '{LeaseId}' with TTL seconds: {TtlSeconds}")]
    public static partial void RequestingLeaseRenewal(this ILogger logger, [SensitiveInformation] string LeaseId, int TtlSeconds);

    [LoggerMessage(EventId = 3011, Level = LogLevel.Warning, Message = "Lease '{LeaseId}' for connection '{ConnectionName}' is not renewable or renewal returned null")]
    public static partial void LeaseNotRenewable(this ILogger logger, [SensitiveInformation] string LeaseId, string ConnectionName);

    [LoggerMessage(EventId = 3012, Level = LogLevel.Information, Message = "Lease '{LeaseId}' renewed for connection '{ConnectionName}' - new DurationSeconds={Duration}, Renewable={Renewable}")]
    public static partial void LeaseRenewed(this ILogger logger, [SensitiveInformation] string LeaseId, string ConnectionName, int Duration, bool Renewable);

    [LoggerMessage(EventId = 3013, Level = LogLevel.Error, Message = "Failed to renew lease '{LeaseId}' for connection '{ConnectionName}': {Message}")]
    public static partial void RenewFailed(this ILogger logger, Exception exception, [SensitiveInformation] string LeaseId, string ConnectionName, string Message);

    [LoggerMessage(EventId = 3014, Level = LogLevel.Debug, Message = "Initializing PostgresClientProvider credentials watcher for connection '{ConnectionName}'")]
    public static partial void InitializingWatcher(this ILogger logger, string ConnectionName);

    [LoggerMessage(EventId = 3015, Level = LogLevel.Information, Message = "PostgresClientProvider credentials watcher started and attached to Vault parent for connection '{ConnectionName}'")]
    public static partial void StartedWatcher(this ILogger logger, string ConnectionName);
}

