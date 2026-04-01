using Minnaloushe.Core.VaultOptions.Vault;

namespace Minnaloushe.Core.VaultOptions.ResolvedOptions;

public sealed class ResolvedKeyedOptions<TOptions>
    : IResolvedKeyedOptions<TOptions>
    where TOptions : VaultStoredOptions
{
    private readonly Dictionary<string, ResolvedOptions<TOptions>> _values = [];

    public IResolvedOptions<TOptions>? Get(string key)
    {
        return _values.GetValueOrDefault(key);
        //throw new InvalidOperationException(
        //    $"{typeof(TOptions).Name} for key '{key}' not initialized.");
    }

    public void Set(string key, TOptions value)
    {
        if (!_values.TryGetValue(key, out var existingValue))
        {
            var wrapper = new ResolvedOptions<TOptions>();
            wrapper.Set(value);
            _values[key] = wrapper;
        }
        else
        {
            existingValue.Set(value); // for initializer retry
        }
    }
}