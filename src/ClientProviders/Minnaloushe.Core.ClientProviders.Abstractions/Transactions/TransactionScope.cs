using System.Data;

namespace Minnaloushe.Core.ClientProviders.Abstractions.Transactions;

// Fast replacement for System.Transactions.TransactionScope, This custom implementation provides a simplified
// transaction scope mechanism that can be used with any ADO.NET provider, but is currently only utilized
// by the PostgreSQL client provider in this library. It manages a single transaction scope per context
// and allows for explicit commit or rollback of transactions, ensuring proper resource cleanup through the IDisposable pattern.
/// <summary>
/// Provides a scope for managing a database transaction, ensuring that all operations within the scope are either
/// committed as a unit or rolled back in case of failure.
/// Only postgres client supports transaction scope for now.
/// </summary>
/// <remarks>Only one TransactionScope can be active at a time within the current context. Use the InitializeScope
/// method to establish a database connection and begin a transaction. Call Commit to persist changes or Rollback to
/// revert them. Disposing the scope will automatically clean up resources. Attempting to initialize a new scope with a
/// different connection name while one is active will result in an InvalidOperationException.</remarks>
public class TransactionScope : IDisposable
{
    internal bool IsDisposed { get; private set; }
    public IDbConnection? Connection { get; set; }
    public IDbTransaction? Transaction { get; set; }
    public string ConnectionName { get; set; } = string.Empty;

    public TransactionScope()
    {
        if (AmbientTransaction.Current != null)
        {
            throw new InvalidOperationException("A transaction scope is already active on the current context.");
        }

        AmbientTransaction.SetScope(this);
    }

    public void Dispose()
    {
        AmbientTransaction.SetScope(null);
        Transaction?.Dispose();
        Connection?.Dispose();
        IsDisposed = true;
    }

    public void Commit()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        Transaction?.Commit();

        CleanUp();
    }

    public void Rollback()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        Transaction?.Rollback();

        CleanUp();
    }

    public IDbConnection InitializeScope(Func<IDbConnection> connectionFactory, string connectionName)
    {
        if (Connection == null)
        {
            Connection = connectionFactory();
            Transaction = Connection.BeginTransaction();
            ConnectionName = connectionName;
        }
        else if (ConnectionName != connectionName)
        {
            throw new InvalidOperationException($"Active transaction scope is using connection '{ConnectionName}', cannot acquire connection for '{connectionName}'.");
        }

        return Connection;
    }

    private void CleanUp()
    {
        Transaction?.Dispose();
        Transaction = null;
        Connection?.Dispose();
        Connection = null;
    }
}