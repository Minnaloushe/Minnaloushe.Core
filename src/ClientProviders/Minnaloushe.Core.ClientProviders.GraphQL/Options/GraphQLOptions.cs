using Minnaloushe.Core.VaultOptions.Vault;

namespace Minnaloushe.Core.ClientProviders.GraphQL.Options;

public record GraphQLOptions: VaultStoredOptions
{
    internal static string SectionName => "GraphQL";
    public string Endpoint { get; init; } = string.Empty;
    public override bool IsEmpty => string.IsNullOrEmpty(Endpoint);

    public override VaultStoredOptions ApplyVaultData(IDictionary<string, object> vaultData)
    {
        return this with
        {
            Endpoint = vaultData.TryGetValue(nameof(Endpoint), out var endpoint)
                ? endpoint.ToString() ?? Endpoint
                : Endpoint
        };
    }
}