namespace Minnaloushe.Core.ClientProviders.RabbitMq;

public record RabbitMqClientOptions
{
    public string Host { get; init; } = string.Empty;
    public ushort Port { get; init; } = 0;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string ServiceName { get; init; } = string.Empty;
}