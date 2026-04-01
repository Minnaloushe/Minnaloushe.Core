using Minnaloushe.Core.ClientProviders.Abstractions;
using MongoDB.Driver;

namespace Minnaloushe.Core.ClientProviders.MongoDb.Abstractions;

//TODO Rename to IMongoDynamicClientProvider derive from IMongoClientProvider. Create IMongoClientProvider that will not contain IObservableCredentialsWatcher
public interface IMongoClientProvider : IClientProvider<IMongoClient>, IObservableCredentialsWatcher
{

}