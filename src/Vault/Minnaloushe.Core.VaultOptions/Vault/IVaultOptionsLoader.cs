namespace Minnaloushe.Core.VaultOptions.Vault;

public interface IVaultOptionsLoader<TOptions>
    where TOptions : VaultStoredOptions
{
    Task<TOptions?> LoadAsync(
        TOptions options,
        CancellationToken cancellationToken = default);
}