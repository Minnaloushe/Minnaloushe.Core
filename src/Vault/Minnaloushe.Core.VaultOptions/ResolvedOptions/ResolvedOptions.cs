using Minnaloushe.Core.VaultOptions.Vault;

namespace Minnaloushe.Core.VaultOptions.ResolvedOptions;

public sealed class ResolvedOptions<TOptions> : IResolvedOptions<TOptions>
    where TOptions : VaultStoredOptions
{
    public TOptions Value
    {
        get =>
        field ?? throw new InvalidOperationException(
            $"{typeof(TOptions).Name} accessed before initialization."); private set;
    }

    public bool IsEmpty { get; private set; }

    internal void Set(TOptions value)
    {
        Value = value;
        IsEmpty = false;
    }
}