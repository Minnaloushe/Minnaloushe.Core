namespace Minnaloushe.Core.ClientProviders.Kafka.Options;

public record KafkaClientOptions
{
    public string ConnectionString { get; init; } = string.Empty;
    public string Host { get; init; } = string.Empty;
    public ushort Port { get; init; } = 0;
    public string ServiceKey { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string ServiceName { get; init; } = string.Empty;
    public KafkaParameters Parameters { get; init; } = new();
}