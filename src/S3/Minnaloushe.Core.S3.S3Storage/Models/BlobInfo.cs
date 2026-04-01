namespace Minnaloushe.Core.S3.S3Storage.Models;

public record BlobInfo
{
    public required string Key { get; init; }
    public required ulong Size { get; init; }
    public DateTimeOffset? LastModified { get; init; }
    public required string ETag { get; init; }
}