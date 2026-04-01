using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.ClientProviders.Abstractions;
using Minnaloushe.Core.ServiceDiscovery.Routines;
using Minnaloushe.Core.VaultOptions.Implementations;
using VaultSharp;

namespace Minnaloushe.Core.VaultOptions.Vault;

public sealed class VaultOptionsLoader<TOptions>(
    IClientProvider<IVaultClient>? vaultClientProvider,
    IOptions<VaultService.Options.VaultOptions> vaultOptions,
    IInfrastructureConventionProvider dependenciesProvider,
    ILogger<VaultOptionsLoader<TOptions>> logger)
    : IVaultOptionsLoader<TOptions>
    where TOptions : VaultStoredOptions
{
    public async Task<TOptions?> LoadAsync(
        TOptions options,
        CancellationToken cancellationToken = default)
    {
        if (!options.IsEmpty)
        {
            logger.LogOptionsAlreadyPopulated(typeof(TOptions).Name);

            return options;
        }

        if (string.IsNullOrWhiteSpace(options.VaultPath))
        {
            throw new InvalidOperationException(
                $"{typeof(TOptions).Name} is empty and VaultPath is not configured.");
        }

        logger.LogLoadingFromVault(
            typeof(TOptions).Name,
            options.VaultPath,
            vaultOptions.Value.MountPoint);

        var client = vaultClientProvider?.Acquire();

        if (!(client?.IsInitialized ?? false))
        {
            logger.LogWarning("Vault client is not initialized yet");
            return null;
        }

        var path = await dependenciesProvider.GetKvSecretPath(options.VaultPath);

        var vaultData = await client.Client.V1.Secrets.KeyValue.V2.ReadSecretAsync(
            path, mountPoint: vaultOptions.Value.MountPoint);

        if (vaultData is null || vaultData.Data.Data.Count == 0)
        {
            throw new InvalidOperationException(
                $"Vault returned no data for path '{options.VaultPath}'.");
        }

        logger.LogRetrievedKeys(
            vaultData.Data.Data.Count,
            string.Join(", ", vaultData.Data.Data.Keys));

        var result = ApplyVaultValues(options, vaultData.Data.Data);

        logger.LogAppliedVaultData(
            typeof(TOptions).Name,
            result.IsEmpty);

        return result;
    }

    private static TOptions ApplyVaultValues(
        TOptions options,
        IDictionary<string, object> vaultData)
    {
        var applied = options.ApplyVaultData(vaultData);

        return applied is not TOptions typedResult
            ? throw new InvalidOperationException(
                $"ApplyVaultData returned {applied.GetType().Name} instead of {typeof(TOptions).Name}")
            : typedResult;
    }
}