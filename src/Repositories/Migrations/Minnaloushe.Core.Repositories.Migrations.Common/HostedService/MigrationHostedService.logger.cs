using Microsoft.Extensions.Logging;

namespace Minnaloushe.Core.Repositories.Migrations.Common.HostedService;

internal static partial class MigrationHostedServiceLogger
{
    [LoggerMessage(EventId = 5300, Level = LogLevel.Information, Message = "Starting database migrations - application startup is blocked until migrations complete")]
    public static partial void StartingMigrations(this ILogger<MigrationHostedService> logger);

    [LoggerMessage(EventId = 5301, Level = LogLevel.Information, Message = "Database migrations completed successfully in {ElapsedSeconds}s - application will now start")]
    public static partial void MigrationsCompleted(this ILogger<MigrationHostedService> logger, double ElapsedSeconds);

    [LoggerMessage(EventId = 5302, Level = LogLevel.Error, Message = "Database migrations were cancelled after {ElapsedSeconds}s - application startup aborted")]
    public static partial void MigrationsCancelled(this ILogger<MigrationHostedService> logger, Exception exception, double ElapsedSeconds);

    [LoggerMessage(EventId = 5303, Level = LogLevel.Critical, Message = "FATAL: Database migrations failed after {ElapsedSeconds}s - application cannot start")]
    public static partial void MigrationsFailed(this ILogger<MigrationHostedService> logger, Exception exception, double ElapsedSeconds);

    [LoggerMessage(EventId = 5304, Level = LogLevel.Debug, Message = "Migration hosted service stopping")]
    public static partial void ServiceStopping(this ILogger<MigrationHostedService> logger);
}
