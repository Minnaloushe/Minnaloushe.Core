using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.Toolbox.AsyncInitializer;
using Minnaloushe.Core.VaultOptions.ResolvedOptions;
using Minnaloushe.Core.VaultOptions.Vault;

namespace Minnaloushe.Core.ClientProviders.Abstractions.VaultExtensions;

/// <summary>
/// Generic base class for initializing multiple keyed client providers with vault-stored options.
/// Handles loading options from vault and initializing each provider instance.
/// </summary>
/// <typeparam name="TOptions">The options type that extends VaultStoredOptions</typeparam>
/// <typeparam name="TProvider">The provider interface type that supports async initialization</typeparam>
public abstract class KeyedClientProvidersInitializer<TOptions, TProvider>(
    IServiceProvider serviceProvider,
    IOptionsMonitor<TOptions> options,
    IVaultOptionsLoader<TOptions> loader,
    ResolvedKeyedOptions<TOptions> resolvedOptions,
    ILogger logger)
    : IAsyncInitializer
    where TOptions : VaultStoredOptions
    where TProvider : class
{
    private readonly List<string> _keys = [];

    /// <summary>
    /// Registers a key for initialization. Called during service registration.
    /// </summary>
    public void RegisterKey(string key)
    {
        _keys.Add(key);
    }

    public async Task<bool> InitializeAsync(CancellationToken cancellationToken)
    {
        LogInitializingClients(_keys.Count);

        var success = true;
        foreach (var key in _keys)
        {
            try
            {
                LogInitializingClient(key);

                var opts = options.Get(key);

                if (opts.IsEmpty)
                {
                    var loaded = await loader.LoadAsync(opts, cancellationToken);

                    if (loaded == null)
                    {
                        success = false;
                        continue;
                    }

                    resolvedOptions.Set(key, loaded);
                }

                var provider = serviceProvider.GetRequiredKeyedService<TProvider>(key);
                await InitializeProviderAsync(provider, cancellationToken);

                LogClientInitialized(key);
            }
            catch (Exception ex)
            {
                LogClientInitializationFailed(key, ex);
                return false;
            }
        }

        LogAllClientsInitialized(_keys.Count);

        return success;
    }

    /// <summary>
    /// Override this method to call the specific initialization method on the provider.
    /// For example: await provider.InitializeAsync(cancellationToken);
    /// </summary>
    protected abstract Task InitializeProviderAsync(TProvider provider, CancellationToken cancellationToken);

    /// <summary>
    /// Override this to provide specific logging. For example: "Initializing {Count} Telegram bots..."
    /// </summary>
    protected abstract void LogInitializingClients(int count);

    /// <summary>
    /// Override this to provide specific logging. For example: "Initializing Telegram bot '{Key}'..."
    /// </summary>
    protected abstract void LogInitializingClient(string key);

    /// <summary>
    /// Override this to provide specific logging. For example: "Telegram bot '{Key}' initialized successfully."
    /// </summary>
    protected abstract void LogClientInitialized(string key);

    /// <summary>
    /// Override this to provide specific logging. For example: "Failed to initialize Telegram bot '{Key}': {Error}"
    /// </summary>
    protected abstract void LogClientInitializationFailed(string key, Exception exception);

    /// <summary>
    /// Override this to provide specific logging. For example: "All {Count} Telegram bots initialized successfully."
    /// </summary>
    protected abstract void LogAllClientsInitialized(int count);

    protected ILogger Logger => logger;
}
