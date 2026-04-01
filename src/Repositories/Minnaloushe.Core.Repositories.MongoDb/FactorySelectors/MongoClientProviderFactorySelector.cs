using Minnaloushe.Core.ClientProviders.Abstractions.FactorySelector;
using Minnaloushe.Core.ClientProviders.MongoDb;
using Minnaloushe.Core.Repositories.Abstractions;
using Minnaloushe.Core.Repositories.MongoDb.Factories;

namespace Minnaloushe.Core.Repositories.MongoDb.FactorySelectors;

/// <summary>
/// Default implementation of factory selector that uses the CanCreate method.
/// </summary>
public class MongoClientProviderFactorySelector
    : DefaultClientProviderFactorySelector<IMongoClientProvider, IMongoClientProviderFactory, RepositoryOptions>,
      IMongoClientProviderFactorySelector
{
}
