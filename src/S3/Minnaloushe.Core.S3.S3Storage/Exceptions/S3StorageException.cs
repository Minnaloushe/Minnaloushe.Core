namespace Minnaloushe.Core.S3.S3Storage.Exceptions;

public class S3StorageException : Exception
{
    public S3StorageException()
    {
    }
    public S3StorageException(string message) : base(message)
    {
    }
    public S3StorageException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public class S3MetadataDeserializationException : S3StorageException
{
    public S3MetadataDeserializationException(string blobKey, string metadataKey) : base($"Failed to deserialize metadata key '{metadataKey}' for blob '{blobKey}'")
    {
        BlobKey = blobKey;
        MetadataKey = metadataKey;
    }

    public string BlobKey { get; init; }
    public string MetadataKey { get; init; }

}

public class S3MetadataNotFoundException : S3StorageException
{
    public S3MetadataNotFoundException(string blobKey) : base($"Metadata not found for blob '{blobKey}'")
    {
        BlobKey = blobKey;
    }

    public string BlobKey { get; init; }
}