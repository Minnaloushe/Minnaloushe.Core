using Minnaloushe.Core.ClientProviders.Abstractions.FactorySelector;
using Minnaloushe.Core.ClientProviders.Postgres;
using Minnaloushe.Core.Repositories.Abstractions;

namespace Minnaloushe.Core.Repositories.Postgres.FactorySelectors;

/// <summary>
/// Selects the appropriate PostgreSQL client provider factory based on repository options.
/// </summary>
public interface IPostgresClientProviderFactorySelector
    : IClientProviderFactorySelector<IPostgresClientProvider, Factories.IPostgresClientProviderFactory, RepositoryOptions>
{
}
