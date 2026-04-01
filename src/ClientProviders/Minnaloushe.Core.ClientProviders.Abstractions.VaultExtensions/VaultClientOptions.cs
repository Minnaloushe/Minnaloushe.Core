namespace Minnaloushe.Core.ClientProviders.Abstractions.VaultExtensions;

public record VaultClientOptions
{
    public string ServiceName { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public TimeSpan LeaseRenewInterval { get; set; } = TimeSpan.FromHours(1);
}
