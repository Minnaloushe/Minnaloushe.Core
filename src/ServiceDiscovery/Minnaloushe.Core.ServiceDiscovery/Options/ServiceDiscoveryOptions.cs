namespace Minnaloushe.Core.ServiceDiscovery.Options;

public record ServiceDiscoveryOptions
{
    public const string SectionName = "ServiceDiscovery";
    public required string ConsulService { get; init; } = "consul";
    public int ConsulPort { get; init; } = 8500;
}