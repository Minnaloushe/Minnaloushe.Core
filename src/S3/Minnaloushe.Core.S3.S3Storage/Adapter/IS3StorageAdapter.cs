using Minnaloushe.Core.S3.S3Storage.Models;

namespace Minnaloushe.Core.S3.S3Storage.Adapter;

public interface IS3StorageAdapter
{
    Task GetStreamAsync(string key, Stream stream, CancellationToken cancellationToken = default);
    Task<BlobInfo> PutAsync(string key, Stream data, IEnumerable<BlobTag>? tags = null, string contentType = "application/octet-stream", CancellationToken cancellationToken = default);
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<BlobInfo>> ListAsync(string? prefix = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<BlobTag>> GetTagsAsync(string key, CancellationToken cancellationToken = default);
    IAsyncEnumerable<BlobInfo> ListBlobsAsync(string prefix = "", CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
    Task<BlobInfo> GetBlobInfoAsync(string existingFileKey, CancellationToken cancellationToken = default);
    Task<BlobInfo> RenameAsync(string existingKey, string newKey, bool overwrite = false, CancellationToken cancellationToken = default);
}