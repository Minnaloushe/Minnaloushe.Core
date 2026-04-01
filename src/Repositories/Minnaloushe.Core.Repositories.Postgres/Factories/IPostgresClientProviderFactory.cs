using Minnaloushe.Core.ClientProviders.Abstractions.Factories;
using Minnaloushe.Core.ClientProviders.Postgres;
using Minnaloushe.Core.Repositories.Abstractions;

namespace Minnaloushe.Core.Repositories.Postgres.Factories;

/// <summary>
/// Factory for creating PostgreSQL client providers.
/// Implementations should inspect the provided options to determine if they can create a provider.
/// </summary>
public interface IPostgresClientProviderFactory : IClientProviderFactory<IPostgresClientProvider, RepositoryOptions>
{
}
