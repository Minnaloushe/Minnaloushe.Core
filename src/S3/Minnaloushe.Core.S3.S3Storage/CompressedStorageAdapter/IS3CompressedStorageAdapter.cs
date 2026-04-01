using Minnaloushe.Core.S3.S3Storage.Models;

namespace Minnaloushe.Core.S3.S3Storage.CompressedStorageAdapter;

public interface IS3CompressedStorageAdapter
{
    Task GetUncompressedAsync(string key, Stream stream, CancellationToken cancellationToken = default);
    Task<BlobInfo> PutCompressedAsync(string key, Stream data, IEnumerable<BlobTag>? tags = null, CancellationToken cancellationToken = default);
}