using Minnaloushe.Core.ClientProviders.Abstractions;
using Minnaloushe.Core.ClientProviders.Abstractions.CredentialsWatcher;
using MongoDB.Driver;

namespace Minnaloushe.Core.ClientProviders.MongoDb;

public interface IMongoClientProvider : IClientProvider<IMongoClient>, IObservableCredentialsWatcher
{

}