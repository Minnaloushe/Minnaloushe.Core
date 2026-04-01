namespace Minnaloushe.Core.Repositories.MongoDb;

public interface ISoftDeleteEntity
{
    DateTimeOffset? DeletedAt { get; }
}