using Minnaloushe.Core.ClientProviders.Abstractions.FactorySelector;
using Minnaloushe.Core.ClientProviders.MongoDb;
using Minnaloushe.Core.Repositories.Abstractions;

namespace Minnaloushe.Core.Repositories.MongoDb.FactorySelectors;

/// <summary>
/// Selects the appropriate MongoDB client provider factory based on repository options.
/// </summary>
public interface IMongoClientProviderFactorySelector
    : IClientProviderFactorySelector<IMongoClientProvider, Factories.IMongoClientProviderFactory, RepositoryOptions>
{
}
