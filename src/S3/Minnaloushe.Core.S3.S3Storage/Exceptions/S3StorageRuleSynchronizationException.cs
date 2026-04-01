namespace Minnaloushe.Core.S3.S3Storage.Exceptions;

public class S3StorageRuleSynchronizationException : S3StorageException
{
    public S3StorageRuleSynchronizationException(string message) : base(message) { }
    public S3StorageRuleSynchronizationException(string message, Exception inner) : base(message, inner) { }
}