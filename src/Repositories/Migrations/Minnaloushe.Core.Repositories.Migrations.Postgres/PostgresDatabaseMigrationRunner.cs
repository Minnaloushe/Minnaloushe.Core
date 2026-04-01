using Microsoft.Extensions.Logging;
using Minnaloushe.Core.Repositories.Migrations.Common;
using Npgsql;
using System.Security.Cryptography;
using System.Text;

namespace Minnaloushe.Core.Repositories.Migrations.Postgres;

/// <summary>
/// Runs migrations for a single PostgreSQL database with distributed locking via advisory locks.
/// Each database gets its own runner instance.
/// </summary>
internal sealed class PostgresDatabaseMigrationRunner(
    NpgsqlConnection connection,
    string databaseName,
    ILogger<PostgresDatabaseMigrationRunner> logger)
{
    private const string MigrationsTableName = "__migrations";

    private readonly long _advisoryLockKey = BitConverter.ToInt64(
        SHA256.HashData(Encoding.UTF8.GetBytes($"migrations_{databaseName}")));

    /// <summary>
    /// Runs all provided migrations for this database.
    /// Acquires a PostgreSQL advisory lock to prevent concurrent migrations.
    /// </summary>
    public async Task RunMigrationsAsync(
        IEnumerable<IMigration> migrations,
        TimeSpan? lockAcquireTimeout = null,
        CancellationToken cancellationToken = default)
    {
        var migrationsList = migrations.ToList();
        if (migrationsList.Count == 0)
        {
            logger.NoMigrationsToRun(databaseName);
            return;
        }

        await EnsureMigrationsTableAsync(cancellationToken).ConfigureAwait(false);

        var owner = Guid.NewGuid().ToString("D");
        lockAcquireTimeout ??= TimeSpan.FromSeconds(30);

        // Open a dedicated connection for advisory lock scope

        // Try to acquire advisory lock with timeout
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(lockAcquireTimeout.Value);

        var acquired = await TryAcquireAdvisoryLockAsync(connection, cts.Token).ConfigureAwait(false);

        if (!acquired)
        {
            throw new InvalidOperationException(
                $"Unable to acquire migrations lock for database '{databaseName}' within {lockAcquireTimeout.Value.TotalSeconds}s timeout.");
        }

        try
        {
            logger.MigrationsLockAcquired(databaseName, owner);

            foreach (var migration in migrationsList.OrderBy(m => m.Id))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var alreadyApplied = await IsMigrationAppliedAsync(connection, migration.Id, cancellationToken)
                    .ConfigureAwait(false);

                if (alreadyApplied)
                {
                    logger.MigrationAlreadyApplied(migration.Id, databaseName);
                    continue;
                }

                logger.ApplyingMigration(migration.Id, databaseName);

                await migration.UpAsync(cancellationToken).ConfigureAwait(false);

                await RecordMigrationAsync(connection, migration.Id, owner, cancellationToken)
                    .ConfigureAwait(false);

                logger.MigrationApplied(migration.Id, databaseName);
            }

            logger.AllMigrationsCompleted(databaseName);
        }
        finally
        {
            await ReleaseAdvisoryLockAsync(connection, cancellationToken).ConfigureAwait(false);
            logger.MigrationsLockReleased(databaseName, owner);
        }
    }

    private async Task EnsureMigrationsTableAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS __migrations (
                id TEXT PRIMARY KEY,
                applied_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                applied_by TEXT NOT NULL
            )
            """;


        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> TryAcquireAdvisoryLockAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var command = new NpgsqlCommand($"SELECT pg_advisory_lock({_advisoryLockKey})", connection);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task ReleaseAdvisoryLockAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var command = new NpgsqlCommand($"SELECT pg_advisory_unlock({_advisoryLockKey})", connection);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Best-effort lock release; advisory locks are released when connection closes
        }
    }

    private static async Task<bool> IsMigrationAppliedAsync(
        NpgsqlConnection connection,
        string migrationId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT COUNT(1) FROM __migrations WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", migrationId);

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(result) > 0;
    }

    private static async Task RecordMigrationAsync(
        NpgsqlConnection connection,
        string migrationId,
        string owner,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "INSERT INTO __migrations (id, applied_at, applied_by) VALUES (@id, @appliedAt, @appliedBy)",
            connection);
        command.Parameters.AddWithValue("id", migrationId);
        command.Parameters.AddWithValue("appliedAt", DateTime.UtcNow);
        command.Parameters.AddWithValue("appliedBy", owner);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
