using Microsoft.Extensions.Logging;

namespace Minnaloushe.Core.Repositories.Migrations.Common.HostedService;

internal static partial class MigrationOrchestratorBaseLogger
{
    [LoggerMessage(EventId = 5200, Level = LogLevel.Information, Message = "No migrations registered")]
    public static partial void NoMigrationsRegistered(this ILogger logger);

    [LoggerMessage(EventId = 5201, Level = LogLevel.Debug, Message = "Starting migrations for {RepositoryCount} repository(s)")]
    public static partial void StartingMigrations(this ILogger logger, int RepositoryCount);

    [LoggerMessage(EventId = 5202, Level = LogLevel.Debug, Message = "Processing {MigrationCount} migration(s) for repository '{RepositoryName}'")]
    public static partial void ProcessingMigrations(this ILogger logger, int MigrationCount, string RepositoryName);

    [LoggerMessage(EventId = 5203, Level = LogLevel.Debug, Message = "No configuration found for repository '{RepositoryName}'. Skipping migrations.")]
    public static partial void NoConfigurationFound(this ILogger logger, string RepositoryName);

    [LoggerMessage(EventId = 5204, Level = LogLevel.Debug, Message = "Migrations disabled for repository '{RepositoryName}'. Skipping. (checked in {ElapsedMs}ms)")]
    public static partial void MigrationsDisabled(this ILogger logger, string RepositoryName, long ElapsedMs);

    [LoggerMessage(EventId = 5205, Level = LogLevel.Debug, Message = "Completed migrations for repository '{RepositoryName}' in {ElapsedSeconds}s")]
    public static partial void RepositoryMigrationsCompleted(this ILogger logger, string RepositoryName, double ElapsedSeconds);

    [LoggerMessage(EventId = 5206, Level = LogLevel.Information, Message = "All database migrations completed successfully in {ElapsedSeconds}s")]
    public static partial void AllMigrationsCompleted(this ILogger logger, double ElapsedSeconds);
}
