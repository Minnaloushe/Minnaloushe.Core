namespace Minnaloushe.Core.Repositories.Migrations.Common;

//TODO: Consider adding support for DownAsync
// For now it is useless since on startup services go only up or fail loudly
/// <summary>
/// Represents a database migration that can be applied to a specific database.
/// </summary>
public interface IMigration
{
    /// <summary>
    /// Unique migration id (used as the applied marker).
    /// Must be stable across builds and unique within a database.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// The database name this migration targets.
    /// Used to group migrations by database.
    /// </summary>
    string TargetRepository { get; }

    /// <summary>
    /// Run the migration against the provided database.
    /// Should be idempotent or safe to re-run if partially applied.
    /// </summary>
    Task UpAsync(CancellationToken cancellationToken);
}