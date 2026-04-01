using Minnaloushe.Core.ClientProviders.Abstractions;
using Minnaloushe.Core.ClientProviders.MongoDb.Models;
using MongoDB.Driver;

namespace Minnaloushe.Core.ClientProviders.MongoDb.Abstractions;

public interface IMongoClientFactory : IClientFactory<IMongoClient, MongoConfig>
{

}