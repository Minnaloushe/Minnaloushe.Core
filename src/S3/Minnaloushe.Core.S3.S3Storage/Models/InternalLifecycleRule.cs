namespace Minnaloushe.Core.S3.S3Storage.Models;

public record InternalLifecycleRule
{
    public required int ExpirationInDays { get; init; }

    public required string Prefix { get; init; }
}