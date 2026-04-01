using Minnaloushe.Core.ClientProviders.Abstractions;
using Minnaloushe.Core.ClientProviders.Abstractions.CredentialsWatcher;
using Npgsql;

namespace Minnaloushe.Core.ClientProviders.Postgres;

public interface IPostgresClientProvider : IClientProvider<NpgsqlConnection>, IObservableCredentialsWatcher
{
}
