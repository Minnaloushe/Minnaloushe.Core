using Microsoft.Extensions.Logging;

namespace Minnaloushe.Core.ClientProviders.Postgres.Implementations;

internal static partial class ConnectionStringPostgresClientProviderLogger
{
    [LoggerMessage(
        EventId = 3102,
        Level = LogLevel.Information,
        Message = "Initializing ConnectionStringPostgresClientProvider for connection '{ConnectionName}'")]
    public static partial void Initializing(
        this ILogger<ConnectionStringPostgresClientProvider> logger,
        string connectionName);

    [LoggerMessage(
        EventId = 3103,
        Level = LogLevel.Debug,
        Message = "Creating PostgreSQL connection from connection string for connection '{ConnectionName}'")]
    public static partial void CreatingConnection(
        this ILogger<ConnectionStringPostgresClientProvider> logger,
        string connectionName);

    [LoggerMessage(
        EventId = 3104,
        Level = LogLevel.Information,
        Message = "ConnectionStringPostgresClientProvider initialized for connection '{ConnectionName}'")]
    public static partial void Initialized(
        this ILogger<ConnectionStringPostgresClientProvider> logger,
        string connectionName);
    [LoggerMessage(EventId = 3100, Level = LogLevel.Debug, Message = "PostgresClientProvider constructed for connection '{ConnectionName}'")]
    public static partial void Constructed(this ILogger<ConnectionStringPostgresClientProvider> logger, string ConnectionName);

    [LoggerMessage(EventId = 3101, Level = LogLevel.Trace, Message = "Acquiring NpgsqlConnection lease for connection '{ConnectionName}'")]
    public static partial void AcquiringLease(this ILogger<ConnectionStringPostgresClientProvider> logger, string ConnectionName);
}


