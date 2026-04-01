using Confluent.Kafka.Admin;
using System.Globalization;

namespace Minnaloushe.Core.ClientProviders.Kafka.Options;

public static class KafkaParametersExtensions
{
    public static TopicSpecification ToTopicSpecification(this TopicConfiguration config, string topicName)
    {
        return new TopicSpecification()
        {
            Name = topicName,
            NumPartitions = config.NumPartitions,
            ReplicationFactor = config.ReplicationFactor,
            Configs = new Dictionary<string, string>()
            {
                { "retention.ms", config.RetentionTime.TotalMilliseconds.ToString(CultureInfo.InvariantCulture) },
                { "retention.bytes", config.RetentionBytes.ToString() },
                { "cleanup.policy", config.CleanUpPolicy.ToConfig() },
                {
                    "delete.retention.ms",
                    config.DeleteRetentionTime.TotalMilliseconds.ToString(CultureInfo.InvariantCulture)
                },
                {
                    "min.compaction.lag.ms",
                    config.MinCompactionLag.TotalMilliseconds.ToString(CultureInfo.InstalledUICulture)
                },
                { "segment.ms", config.Segment.TotalMilliseconds.ToString(CultureInfo.InstalledUICulture) },
                { "min.cleanable.dirty.ratio", config.MinCleanableDirtyRatio.ToString("F2") }
            }
        };
    }

    public static string ToConfig(this CleanUpPolicy policy)
        => policy switch
        {
            CleanUpPolicy.Delete => "delete",
            CleanUpPolicy.Compact => "compact",
            CleanUpPolicy.CompactAndDelete => "delete,compact",
            _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, null)
        };
}