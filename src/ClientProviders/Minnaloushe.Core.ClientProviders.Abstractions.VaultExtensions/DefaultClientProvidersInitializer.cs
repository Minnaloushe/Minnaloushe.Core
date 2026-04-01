using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.Toolbox.AsyncInitializer;
using Minnaloushe.Core.VaultOptions.ResolvedOptions;
using Minnaloushe.Core.VaultOptions.Vault;

namespace Minnaloushe.Core.ClientProviders.Abstractions.VaultExtensions;

public sealed class DefaultClientProvidersInitializer<TProvider, TOptions>(
    IServiceProvider serviceProvider,
    IOptionsMonitor<TOptions> options,
    IVaultOptionsLoader<TOptions> loader,
    ResolvedKeyedOptions<TOptions> resolvedOptions,
    ILogger<DefaultClientProvidersInitializer<TProvider, TOptions>> logger)
    : IAsyncInitializer
    where TProvider : IAsyncInitializer
    where TOptions : VaultStoredOptions
{
    private readonly List<string> _keys = [];

    public void RegisterKey(string botKey)
    {
        _keys.Add(botKey);
    }

    public async Task<bool> InitializeAsync(CancellationToken cancellationToken)
    {
        logger.LogInitializingSections(_keys.Count);

        var success = true;

        foreach (var key in _keys)
        {
            try
            {
                logger.LogInitializingSection(key);

                var opts = options.Get(key);

                if (opts.IsEmpty)
                {
                    var loaded = await loader.LoadAsync(opts, cancellationToken);
                    if (loaded == null)
                    {
                        logger.LogWarning("{TypeName} failed to load options", loader.GetType().Name);
                        success = false;
                        continue;
                    }

                    resolvedOptions.Set(key, loaded);
                }
                else
                {
                    resolvedOptions.Set(key, opts);
                }


                var provider = serviceProvider.GetRequiredKeyedService<TProvider>(key);
                await provider.InitializeAsync(cancellationToken);

                logger.LogSectionInitialized(key);
            }
            catch (Exception ex)
            {
                logger.LogSectionInitializationFailed(key, ex);
                return false;
            }
        }

        logger.LogAllSectionsInitialized(_keys.Count);
        return success;
    }
}