using Minnaloushe.Core.VaultOptions.Vault;

namespace Minnaloushe.Core.VaultOptions.ResolvedOptions;

public interface IResolvedKeyedOptions<out TOptions>
    where TOptions : VaultStoredOptions
{
    IResolvedOptions<TOptions>? Get(string key);
}