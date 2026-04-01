using Confluent.Kafka;

namespace Minnaloushe.Core.ClientProviders.Kafka.Options;

public record KafkaParameters
{
    public KafkaEngineType EngineType { get; init; } = KafkaEngineType.Reliable;
    public int MaxPollIntervalMs { get; init; } = 300_000;
    public bool EnableAutoCommit { get; init; } = false;
    public int SessionTimeoutMs { get; init; } = 45_000;
    public AutoOffsetReset AutoOffsetReset { get; init; } = AutoOffsetReset.Earliest;
    public TopicConfiguration TopicConfiguration { get; init; } = new();
    public TopicConfiguration DltTopicConfiguration { get; init; } = new();
}