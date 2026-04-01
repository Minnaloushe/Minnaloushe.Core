using Minnaloushe.Core.ClientProviders.Abstractions.FactorySelector;
using Minnaloushe.Core.ClientProviders.Postgres;
using Minnaloushe.Core.Repositories.Abstractions;
using Minnaloushe.Core.Repositories.Postgres.Factories;

namespace Minnaloushe.Core.Repositories.Postgres.FactorySelectors;

/// <summary>
/// Default implementation of factory selector that uses the CanCreate method.
/// </summary>
public class PostgresClientProviderFactorySelector
    : DefaultClientProviderFactorySelector<IPostgresClientProvider, IPostgresClientProviderFactory, RepositoryOptions>,
      IPostgresClientProviderFactorySelector
{
}
