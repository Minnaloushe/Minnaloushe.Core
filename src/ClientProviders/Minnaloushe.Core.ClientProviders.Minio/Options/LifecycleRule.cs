namespace Minnaloushe.Core.ClientProviders.Minio.Options;

public record LifecycleRule
{
    public required int ExpirationInDays { get; init; }
    public required string Prefix { get; init; }
}