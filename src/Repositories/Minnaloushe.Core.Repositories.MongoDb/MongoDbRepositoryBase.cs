using Minnaloushe.Core.Repositories.Abstractions;
using MongoDB.Driver;

namespace Minnaloushe.Core.Repositories.MongoDb;

public abstract class MongoDbRepositoryBase
    (
    RepositoryOptions options
    )
{
    protected string DatabaseName => options.DatabaseName;

    protected IMongoCollection<TDocument> GetCollection<TDocument>(IMongoClient client)
    {
        return client.GetDatabase(DatabaseName).GetCollection<TDocument>(typeof(TDocument).Name);
    }
}