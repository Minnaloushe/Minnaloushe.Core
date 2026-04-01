namespace Minnaloushe.Core.S3.S3Storage.MetadataAdapter;

public interface IS3MetadataAdapter
{
    Task GetMetadataStreamAsync(string blobKey, string metadataKey, Stream outStream, CancellationToken cancellationToken = default);
    Task<T?> GetMetadataAsync<T>(string blobKey, string metadataKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<string>> ListMetadataKeysAsync(string blobKey, CancellationToken cancellationToken = default);
    Task PutMetadataStreamAsync(string blobKey, string metadataKey, Stream data, CancellationToken cancellationToken = default);
    Task PutMetadataAsync<T>(string blobKey, string metadataKey, T metadata, CancellationToken cancellationToken = default);
    Task DeleteMetadataAsync(string blobKey, string metadataKey, CancellationToken cancellationToken = default);
    Task DeleteAllMetadataAsync(string blobKey, CancellationToken cancellationToken = default);
}