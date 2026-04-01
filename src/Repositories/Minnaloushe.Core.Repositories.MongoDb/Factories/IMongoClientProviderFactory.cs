using Minnaloushe.Core.ClientProviders.Abstractions.Factories;
using Minnaloushe.Core.ClientProviders.MongoDb;
using Minnaloushe.Core.Repositories.Abstractions;

namespace Minnaloushe.Core.Repositories.MongoDb.Factories;

/// <summary>
/// Factory for creating MongoDB client providers.
/// Implementations should inspect the provided options to determine if they can create a provider.
/// </summary>
public interface IMongoClientProviderFactory : IClientProviderFactory<IMongoClientProvider, RepositoryOptions>
{
}
