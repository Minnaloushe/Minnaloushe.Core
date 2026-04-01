using Minnaloushe.Core.Toolbox.RetryRoutines.Options;

namespace Minnaloushe.Core.VaultService.Options;

public record VaultOptions
{
    internal const string SectionName = "Vault";
    /// <summary>
    /// Vault address with port and scheme, e.g. http://vault:8200
    /// </summary>
    public required string Address { get; init; } = string.Empty;
    /// <summary>
    /// Vault token
    /// </summary>
    public required string Token { get; init; } = string.Empty;
    /// <summary>
    /// Vault service name in Consul
    /// </summary>
    public required string ServiceName { get; init; } = "vault-vault";
    /// <summary>
    /// Vault scheme (http or https)
    /// </summary>
    public required string Scheme { get; init; } = "http";

    public RetryPolicyOptions RetryPolicy { get; init; } = new();
    public TimeSpan RenewalInterval { get; init; } = TimeSpan.FromMinutes(1);
    public string MountPoint { get; init; } = "kv";
}
