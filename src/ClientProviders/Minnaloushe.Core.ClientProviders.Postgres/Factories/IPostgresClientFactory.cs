using Minnaloushe.Core.ClientProviders.Abstractions.Factories;
using Minnaloushe.Core.ClientProviders.Postgres.Models;
using Npgsql;

namespace Minnaloushe.Core.ClientProviders.Postgres.Factories;

public interface IPostgresClientFactory : IClientFactory<NpgsqlDataSource, PostgresConfig>
{
}
