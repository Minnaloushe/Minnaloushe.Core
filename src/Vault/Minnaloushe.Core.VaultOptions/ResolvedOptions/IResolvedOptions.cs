using Minnaloushe.Core.VaultOptions.Vault;

namespace Minnaloushe.Core.VaultOptions.ResolvedOptions;

public interface IResolvedOptions<out TOptions>
    where TOptions : VaultStoredOptions
{
    TOptions Value { get; }
    bool IsEmpty { get; }
}