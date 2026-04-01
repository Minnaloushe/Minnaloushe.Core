namespace Minnaloushe.Core.ClientProviders.Kafka.Options;

public record TopicConfiguration
{
    public int NumPartitions { get; init; } = 1;
    public short ReplicationFactor { get; init; } = 1;
    public TimeSpan RetentionTime { get; init; } = TimeSpan.FromDays(7);
    public long RetentionBytes { get; init; } = -1;
    public CleanUpPolicy CleanUpPolicy { get; init; } = CleanUpPolicy.Delete;
    public TimeSpan DeleteRetentionTime { get; init; } = TimeSpan.FromDays(1);
    public TimeSpan MinCompactionLag { get; init; } = TimeSpan.FromDays(7);
    public TimeSpan Segment { get; init; } = TimeSpan.FromDays(1);
    public decimal MinCleanableDirtyRatio { get; init; } = 0.1m;

}