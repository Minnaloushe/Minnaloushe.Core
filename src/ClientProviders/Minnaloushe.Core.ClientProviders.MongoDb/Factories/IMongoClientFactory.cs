using Minnaloushe.Core.ClientProviders.Abstractions.Factories;
using Minnaloushe.Core.ClientProviders.MongoDb.Models;
using MongoDB.Driver;

namespace Minnaloushe.Core.ClientProviders.MongoDb.Factories;

public interface IMongoClientFactory : IClientFactory<IMongoClient, MongoConfig>
{

}