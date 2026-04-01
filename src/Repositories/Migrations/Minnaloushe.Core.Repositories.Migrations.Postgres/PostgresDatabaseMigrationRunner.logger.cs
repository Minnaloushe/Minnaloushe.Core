using Microsoft.Extensions.Logging;

namespace Minnaloushe.Core.Repositories.Migrations.Postgres;

internal static partial class PostgresDatabaseMigrationRunnerLogger
{
    [LoggerMessage(EventId = 5100, Level = LogLevel.Information, Message = "No migrations to run for database '{DatabaseName}'")]
    public static partial void NoMigrationsToRun(this ILogger<PostgresDatabaseMigrationRunner> logger, string DatabaseName);

    [LoggerMessage(EventId = 5101, Level = LogLevel.Information, Message = "Migrations lock acquired for database '{DatabaseName}' by {Owner}")]
    public static partial void MigrationsLockAcquired(this ILogger<PostgresDatabaseMigrationRunner> logger, string DatabaseName, string Owner);

    [LoggerMessage(EventId = 5102, Level = LogLevel.Debug, Message = "Skipping already applied migration '{MigrationId}' for database '{DatabaseName}'")]
    public static partial void MigrationAlreadyApplied(this ILogger<PostgresDatabaseMigrationRunner> logger, string MigrationId, string DatabaseName);

    [LoggerMessage(EventId = 5103, Level = LogLevel.Information, Message = "Applying migration '{MigrationId}' to database '{DatabaseName}'")]
    public static partial void ApplyingMigration(this ILogger<PostgresDatabaseMigrationRunner> logger, string MigrationId, string DatabaseName);

    [LoggerMessage(EventId = 5104, Level = LogLevel.Information, Message = "Migration '{MigrationId}' applied successfully to database '{DatabaseName}'")]
    public static partial void MigrationApplied(this ILogger<PostgresDatabaseMigrationRunner> logger, string MigrationId, string DatabaseName);

    [LoggerMessage(EventId = 5105, Level = LogLevel.Information, Message = "All migrations completed successfully for database '{DatabaseName}'")]
    public static partial void AllMigrationsCompleted(this ILogger<PostgresDatabaseMigrationRunner> logger, string DatabaseName);

    [LoggerMessage(EventId = 5106, Level = LogLevel.Information, Message = "Migrations lock released for database '{DatabaseName}' by {Owner}")]
    public static partial void MigrationsLockReleased(this ILogger<PostgresDatabaseMigrationRunner> logger, string DatabaseName, string Owner);
}
