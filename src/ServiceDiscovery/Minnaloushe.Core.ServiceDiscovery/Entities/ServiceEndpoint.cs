namespace Minnaloushe.Core.ServiceDiscovery.Entities;

public record ServiceEndpoint
{
    public string Host { get; init; } = string.Empty;
    public ushort Port { get; init; }
};
