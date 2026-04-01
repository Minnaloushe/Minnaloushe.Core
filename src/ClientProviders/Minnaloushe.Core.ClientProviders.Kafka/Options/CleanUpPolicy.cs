namespace Minnaloushe.Core.ClientProviders.Kafka.Options;

public enum CleanUpPolicy
{
    Compact = 1,
    Delete = 2,
    CompactAndDelete = Compact | Delete
}