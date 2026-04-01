using Microsoft.Extensions.Logging;
using Minnaloushe.Core.Logging.Redaction;

namespace Minnaloushe.Core.ClientProviders.MongoDb.Implementations;

internal static partial class MongoClientProviderLogger
{
    [LoggerMessage(EventId = 4000, Level = LogLevel.Debug, Message = "MongoClientProvider constructed for connection '{ConnectionName}'")]
    public static partial void Constructed(this ILogger logger, string connectionName);

    [LoggerMessage(EventId = 4001, Level = LogLevel.Trace, Message = "Acquiring IMongoClient lease for connection '{ConnectionName}'")]
    public static partial void AcquiringLease(this ILogger logger, string connectionName);

    [LoggerMessage(EventId = 4002, Level = LogLevel.Debug, Message = "Issuing MongoDB credentials for connection '{ConnectionName}'")]
    public static partial void IssuingCredentials(this ILogger logger, string connectionName);

    [LoggerMessage(EventId = 4003, Level = LogLevel.Trace, Message = "Acquired Vault client lease to request DB credentials for connection '{ConnectionName}'")]
    public static partial void AcquiredVaultClientLease(this ILogger logger, string connectionName);

    [LoggerMessage(EventId = 4004, Level = LogLevel.Debug, Message = "Repository options for '{ConnectionName}': ServiceName={ServiceName}, DatabaseName={DatabaseName}")]
    public static partial void RepositoryOptions(this ILogger logger, string connectionName, string serviceName, string databaseName);

    [LoggerMessage(EventId = 4005, Level = LogLevel.Debug, Message = "Resolved database role for service '{ServiceName}': {Role}")]
    public static partial void ResolvedDatabaseRole(this ILogger logger, string serviceName, string role);

    [LoggerMessage(EventId = 4006, Level = LogLevel.Information, Message = "Received MongoDB credentials lease: LeaseId={LeaseId}, DurationSeconds={Duration}, Renewable={Renewable} for connection '{ConnectionName}'")]
    public static partial void ReceivedCredentialsLease(this ILogger logger, [SensitiveInformation] string LeaseId, int duration, bool renewable, string connectionName);

    [LoggerMessage(EventId = 4007, Level = LogLevel.Information, Message = "Rotated IMongoClient for connection '{ConnectionName}' (Service={ServiceName}, Database={DatabaseName})")]
    public static partial void RotatedClient(this ILogger logger, string connectionName, string serviceName, string databaseName);

    [LoggerMessage(EventId = 4008, Level = LogLevel.Error, Message = "Failed to issue MongoDB credentials for connection '{ConnectionName}': {Message}")]
    public static partial void IssueFailed(this ILogger logger, Exception ex, string connectionName, string message);

    [LoggerMessage(EventId = 4009, Level = LogLevel.Debug, Message = "Renewing MongoDB credentials lease '{LeaseId}' for connection '{ConnectionName}'")]
    public static partial void RenewingLease(this ILogger logger, [SensitiveInformation] string LeaseId, string connectionName);

    [LoggerMessage(EventId = 4010, Level = LogLevel.Trace, Message = "Requesting lease renewal for '{LeaseId}' with TTL seconds: {TtlSeconds}")]
    public static partial void RequestingLeaseRenewal(this ILogger logger, [SensitiveInformation] string LeaseId, int ttlSeconds);

    [LoggerMessage(EventId = 4011, Level = LogLevel.Warning, Message = "Lease '{LeaseId}' for connection '{ConnectionName}' is not renewable or renewal returned null")]
    public static partial void LeaseNotRenewable(this ILogger logger, [SensitiveInformation] string LeaseId, string connectionName);

    [LoggerMessage(EventId = 4012, Level = LogLevel.Information, Message = "Lease '{LeaseId}' renewed for connection '{ConnectionName}' - new DurationSeconds={Duration}, Renewable={Renewable}")]
    public static partial void LeaseRenewed(this ILogger logger, [SensitiveInformation] string LeaseId, string connectionName, int duration, bool renewable);

    [LoggerMessage(EventId = 4013, Level = LogLevel.Error, Message = "Failed to renew lease '{LeaseId}' for connection '{ConnectionName}': {Message}")]
    public static partial void RenewFailed(this ILogger logger, Exception ex, [SensitiveInformation] string LeaseId, string connectionName, string message);

    [LoggerMessage(EventId = 4014, Level = LogLevel.Debug, Message = "Initializing MongoClientProvider credentials watcher for connection '{ConnectionName}'")]
    public static partial void InitializingWatcher(this ILogger logger, string connectionName);

    [LoggerMessage(EventId = 4015, Level = LogLevel.Information, Message = "MongoClientProvider credentials watcher started and attached to Vault parent for connection '{ConnectionName}'")]
    public static partial void StartedWatcher(this ILogger logger, string connectionName);
}