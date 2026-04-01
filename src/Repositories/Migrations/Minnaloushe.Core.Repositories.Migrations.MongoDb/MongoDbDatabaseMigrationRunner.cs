using Inpx.Processor.Repositories.Migrations;
using Microsoft.Extensions.Logging;
using Minnaloushe.Core.Repositories.Migrations.Common;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Minnaloushe.Core.Repositories.Migrations.MongoDb;

/// <summary>
/// Runs migrations for a single database with distributed locking.
/// Each database gets its own runner instance.
/// </summary>
internal sealed class MongoDbDatabaseMigrationRunner(
    IMongoDatabase database,
    ILogger<MongoDbDatabaseMigrationRunner> logger)
{
    private readonly IMongoDatabase _database = database ?? throw new ArgumentNullException(nameof(database));
    private readonly ILogger<MongoDbDatabaseMigrationRunner> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly string _databaseName = database.DatabaseNamespace.DatabaseName;

    private const string MigrationsCollectionName = "__migrations";
    private const string MigrationsLockCollectionName = "__migrations_lock";
    private const string LockId = "migration_lock";

    /// <summary>
    /// Runs all provided migrations for this database.
    /// Acquires a distributed lock to prevent concurrent migrations.
    /// </summary>
    public async Task RunMigrationsAsync(
        IEnumerable<IMigration> migrations,
        TimeSpan? lockAcquireTimeout = null,
        TimeSpan? lockStaleTimeout = null,
        CancellationToken cancellationToken = default)
    {
        var migrationsList = migrations.ToList();
        if (migrationsList.Count == 0)
        {
            _logger.NoMigrationsToRun(_databaseName);
            return;
        }

        var migrationsCollection = _database.GetCollection<BsonDocument>(MigrationsCollectionName);
        var locksCollection = _database.GetCollection<BsonDocument>(MigrationsLockCollectionName);

        var owner = Guid.NewGuid().ToString("D");
        lockAcquireTimeout ??= TimeSpan.FromSeconds(30);
        lockStaleTimeout ??= TimeSpan.FromMinutes(5);

        // Try to acquire lock with timeout
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(lockAcquireTimeout.Value);

        var acquired = await TryAcquireLockAsync(
            locksCollection,
            owner,
            lockStaleTimeout.Value,
            cts.Token).ConfigureAwait(false);

        if (!acquired)
        {
            throw new InvalidOperationException(
                $"Unable to acquire migrations lock for database '{_databaseName}' within {lockAcquireTimeout.Value.TotalSeconds}s timeout.");
        }

        try
        {
            _logger.MigrationsLockAcquired(_databaseName, owner);

            foreach (var migration in migrationsList.OrderBy(m => m.Id))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var idFilter = Builders<BsonDocument>.Filter.Eq("_id", migration.Id);
                var alreadyApplied = await migrationsCollection
                    .Find(idFilter)
                    .AnyAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (alreadyApplied)
                {
                    _logger.MigrationAlreadyApplied(migration.Id, _databaseName);
                    continue;
                }

                _logger.ApplyingMigration(migration.Id, _databaseName);

                await migration.UpAsync(cancellationToken).ConfigureAwait(false);

                var appliedDoc = new BsonDocument
                {
                    { "_id", migration.Id },
                    { "appliedAt", DateTime.UtcNow },
                    { "appliedBy", owner }
                };

                await migrationsCollection
                    .InsertOneAsync(appliedDoc, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                _logger.MigrationApplied(migration.Id, _databaseName);
            }

            _logger.AllMigrationsCompleted(_databaseName);
        }
        finally
        {
            await ReleaseLockAsync(locksCollection, owner, cancellationToken).ConfigureAwait(false);
            _logger.MigrationsLockReleased(_databaseName, owner);
        }
    }

    private static async Task<bool> TryAcquireLockAsync(
        IMongoCollection<BsonDocument> locksCollection,
        string owner,
        TimeSpan staleTimeout,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var staleCutoff = now - staleTimeout;

        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("_id", LockId),
            Builders<BsonDocument>.Filter.Or(
                Builders<BsonDocument>.Filter.Eq("locked", false),
                Builders<BsonDocument>.Filter.Exists("locked", false),
                Builders<BsonDocument>.Filter.Lt("acquiredAt", staleCutoff)
            )
        );

        var update = Builders<BsonDocument>.Update
            .Set("locked", true)
            .Set("owner", owner)
            .Set("acquiredAt", now);

        var options = new FindOneAndUpdateOptions<BsonDocument>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.After
        };

        try
        {
            var result = await locksCollection
                .FindOneAndUpdateAsync(filter, update, options, cancellationToken)
                .ConfigureAwait(false);

            return result != null && result.GetValue("owner", BsonNull.Value).AsString == owner;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private static async Task ReleaseLockAsync(
        IMongoCollection<BsonDocument> locksCollection,
        string owner,
        CancellationToken cancellationToken)
    {
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("_id", LockId),
            Builders<BsonDocument>.Filter.Eq("owner", owner)
        );

        var update = Builders<BsonDocument>.Update
            .Set("locked", false)
            .Unset("owner")
            .Unset("acquiredAt");

        try
        {
            await locksCollection
                .UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = false }, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Swallow exceptions during lock release
            // Logger is called by the caller in the finally block
        }
    }
}