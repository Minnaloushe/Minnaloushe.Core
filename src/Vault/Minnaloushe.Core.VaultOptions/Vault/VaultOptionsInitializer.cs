using Microsoft.Extensions.Options;
using Minnaloushe.Core.Toolbox.AsyncInitializer;
using Minnaloushe.Core.VaultOptions.ResolvedOptions;

namespace Minnaloushe.Core.VaultOptions.Vault;

public sealed class VaultOptionsInitializer<TOptions>(
    IOptions<TOptions> boundOptions,
    IVaultOptionsLoader<TOptions> vaultLoader,
    ResolvedOptions<TOptions> resolved)
    : IAsyncInitializer
    where TOptions : VaultStoredOptions
{
    public async Task<bool> InitializeAsync(CancellationToken cancellationToken)
    {
        var options = boundOptions.Value;

        if (options.IsEmpty)
        {
            var loaded = await vaultLoader.LoadAsync(options, cancellationToken);

            if (loaded == null)
            {
                return false;
            }

            resolved.Set(loaded);
        }

        return true;
    }
}