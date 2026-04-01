using Microsoft.Extensions.Logging;

namespace Minnaloushe.Core.VaultService.Implementations;

public static partial class VaultClientFactoryLogger
{
    [LoggerMessage(LogLevel.Debug, "CreateAsync called for VaultClientFactory")]
    public static partial void LogCreateAsyncCalledForVaultClientFactory(this ILogger logger);

    [LoggerMessage(LogLevel.Debug, "No explicit Vault address configured, attempting service discovery for service name '{ServiceName}'")]
    public static partial void LogNoExplicitVaultAddressConfiguredAttemptingServiceDiscoveryForServiceNameServiceName(this ILogger logger, string ServiceName);

    [LoggerMessage(LogLevel.Debug, "Resolved Vault endpoint: {Address}:{Port}")]
    public static partial void LogResolvedVaultEndpointAddressPort(this ILogger logger, string Address, int Port);

    [LoggerMessage(LogLevel.Information, "Constructed Vault address via discovery: {Address}")]
    public static partial void LogConstructedVaultAddressViaDiscoveryAddress(this ILogger logger, string Address);

    [LoggerMessage(LogLevel.Debug, "Using configured Vault address: {AddressConfigured}")]
    public static partial void LogUsingConfiguredVaultAddress(this ILogger logger, string AddressConfigured);

    [LoggerMessage(LogLevel.Information, "Using token-based Vault authentication (token provided)")]
    public static partial void LogUsingTokenBasedVaultAuthenticationTokenProvided(this ILogger logger);

    [LoggerMessage(LogLevel.Debug, "No token provided in options - checking for Kubernetes service account token at {Path}")]
    public static partial void LogNoTokenProvidedInOptionsCheckingForKubernetesServiceAccountTokenAtPath(this ILogger logger, string Path);

    [LoggerMessage(LogLevel.Information, "Using Kubernetes auth for Vault (service account token present). Application name: {AppName}")]
    public static partial void LogUsingKubernetesAuthForVaultServiceAccountTokenPresentApplicationNameAppName(this ILogger logger, string AppName);

    [LoggerMessage(LogLevel.Error, "Failed to determine Vault authentication method - neither token nor Kubernetes service account token are available")]
    public static partial void LogFailedToDetermineVaultAuthenticationMethodNeitherTokenNorKubernetesServiceAccountToken(this ILogger logger);

    [LoggerMessage(LogLevel.Debug, "Created vault client on {Address}, auth method: {AuthMethod}")]
    public static partial void LogCreatedVaultClient(this ILogger logger, string Address, string AuthMethod);

    [LoggerMessage(3000, LogLevel.Debug, "VaultClientFactory CreateAsync completed in {ElapsedMs}ms.")]
    public static partial void LogCreateAsyncElapsed(this ILogger logger, double ElapsedMs);

    [LoggerMessage(3001, LogLevel.Debug, "Service discovery for Vault resolved in {ElapsedMs}ms.")]
    public static partial void LogServiceDiscoveryResolvedElapsed(this ILogger logger, double ElapsedMs);

    [LoggerMessage(3002, LogLevel.Debug, "Read Kubernetes service account token in {ElapsedMs}ms.")]
    public static partial void LogReadKubernetesTokenElapsed(this ILogger logger, double ElapsedMs);

    [LoggerMessage(3003, LogLevel.Debug, "Retrieved application name in {ElapsedMs}ms.")]
    public static partial void LogGetApplicationNameElapsed(this ILogger logger, double ElapsedMs);

    [LoggerMessage(LogLevel.Information, "Vault client created successfully for address {Address}")]
    public static partial void LogVaultClientCreatedSuccessfullyForAddressAddress(this ILogger logger, string Address);

    [LoggerMessage(LogLevel.Error, "Failed to create Vault client: {Message}")]
    public static partial void LogFailedToCreateVaultClientMessage(this ILogger logger, Exception ex, string Message);
}