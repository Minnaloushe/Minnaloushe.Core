using Minnaloushe.Core.ClientProviders.Abstractions.ClientLease;
using Minnaloushe.Core.ClientProviders.Abstractions.CredentialsWatcher;
using Minnaloushe.Core.ClientProviders.Abstractions.Transactions;
using Npgsql;

namespace Minnaloushe.Core.ClientProviders.Postgres;

//TODO Refactor this. Database  client provider logic needs refactoring
internal static class TransactionAcquisitionHelper
{
    public static IClientLease<NpgsqlConnection> Acquire(IRenewableClientHolder<NpgsqlDataSource> clientHolder, string connectionName)
    {
        if (AmbientTransaction.Current != null)
        {
            var scope = AmbientTransaction.Current;

            var connection = scope.InitializeScope(() =>
            {
                using var lease = clientHolder.Acquire();
                return lease.Client.OpenConnection();
            }, connectionName);

            var result = connection as NpgsqlConnection ??
                         throw new InvalidOperationException(
                             "The connection returned from the scope initializer was not of the expected type.");
            return new SharedClientLease<NpgsqlConnection>(result);
        }
        using var lease = clientHolder.Acquire();

        return new StandAloneLease<NpgsqlConnection>(lease.Client.OpenConnection());
    }
}