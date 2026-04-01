using Microsoft.Extensions.Logging;

namespace Minnaloushe.Core.VaultOptions.Implementations;

internal static partial class VaultOptionsLoaderLogger
{
    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Options {OptionsType} are already populated; skipping Vault lookup.")]
    public static partial void LogOptionsAlreadyPopulated(
        this ILogger logger,
        string optionsType);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Loading {OptionsType} from Vault path '{VaultPath}' with mount point '{MountPoint}'.")]
    public static partial void LogLoadingFromVault(
        this ILogger logger,
        string optionsType,
        string vaultPath,
        string mountPoint);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Retrieved {Count} keys from Vault: {Keys}")]
    public static partial void LogRetrievedKeys(
        this ILogger logger,
        int count,
        string keys);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Applied Vault data to {OptionsType}. Result IsEmpty: {IsEmpty}")]
    public static partial void LogAppliedVaultData(
        this ILogger logger,
        string optionsType,
        bool isEmpty);
}
