namespace Minnaloushe.Core.VaultOptions.Vault;

public abstract record VaultStoredOptions
{
    public abstract bool IsEmpty { get; }
    public string VaultPath { get; init; } = string.Empty;
    public abstract VaultStoredOptions ApplyVaultData(IDictionary<string, object> vaultData);
}