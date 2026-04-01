using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.K8s.Routines;
using Minnaloushe.Core.ServiceDiscovery.Abstractions;
using Minnaloushe.Core.ServiceDiscovery.Routines;
using Minnaloushe.Core.VaultService.Implementations;
using Minnaloushe.Core.VaultService.Options;
using System.Diagnostics;
using VaultSharp;
using VaultSharp.V1.AuthMethods;
using VaultSharp.V1.AuthMethods.Kubernetes;
using VaultSharp.V1.AuthMethods.Token;

namespace Minnaloushe.Core.VaultService.Factory;

internal class VaultClientFactory(
    IOptionsMonitor<VaultOptions> options,
    ILogger<VaultClientFactory> logger,
    IInfrastructureConventionProvider applicationRoutines,
    IServiceDiscoveryService? serviceDiscovery = null)
    : IVaultClientFactory
{
    public async Task<IVaultClient> CreateAsync(object? config, CancellationToken cancellationToken)
    {
        logger.LogCreateAsyncCalledForVaultClientFactory();

        try
        {
            //TODO investigate DefaultName
            var currentOptions = options.CurrentValue;

            var address = currentOptions.Address;

            if (string.IsNullOrWhiteSpace(address) && serviceDiscovery is not null)
            {
                logger.LogNoExplicitVaultAddressConfiguredAttemptingServiceDiscoveryForServiceNameServiceName(currentOptions.ServiceName);

                var swDiscovery = Stopwatch.StartNew();
                var endpoints = await serviceDiscovery.ResolveServiceEndpoint(currentOptions.ServiceName, cancellationToken);
                swDiscovery.Stop();

                logger.LogServiceDiscoveryResolvedElapsed(swDiscovery.Elapsed.TotalMilliseconds);

                var ep = endpoints?.FirstOrDefault()
                         ?? throw new InvalidOperationException("Failed to discover Vault service endpoint");

                logger.LogResolvedVaultEndpointAddressPort(ep.Host, ep.Port);

                var scheme = string.IsNullOrWhiteSpace(currentOptions.Scheme)
                    ? "http"
                    : currentOptions.Scheme;

                address = $"{scheme}://{ep.Host}:{ep.Port}";
                logger.LogConstructedVaultAddressViaDiscoveryAddress(address);
            }
            else
            {
                logger.LogUsingConfiguredVaultAddress(address);
            }

            IAuthMethodInfo authMethod;

            if (!string.IsNullOrWhiteSpace(currentOptions.Token))
            {
                // Do NOT log the token value
                logger.LogUsingTokenBasedVaultAuthenticationTokenProvided();
                authMethod = new TokenAuthMethodInfo(currentOptions.Token);
            }
            else
            {
                // Try Kubernetes JWT if available (common in k8s pods)
                logger.LogNoTokenProvidedInOptionsCheckingForKubernetesServiceAccountTokenAtPath(KubernetesConstants.ServiceAccountTokenPath);
                if (File.Exists(KubernetesConstants.ServiceAccountTokenPath))
                {
                    var swToken = Stopwatch.StartNew();
                    var jwt = await File.ReadAllTextAsync(KubernetesConstants.ServiceAccountTokenPath, cancellationToken);
                    swToken.Stop();
                    logger.LogReadKubernetesTokenElapsed(swToken.Elapsed.TotalMilliseconds);

                    var swAppName = Stopwatch.StartNew();
                    var appName = await applicationRoutines.GetApplicationName();
                    swAppName.Stop();
                    logger.LogGetApplicationNameElapsed(swAppName.Elapsed.TotalMilliseconds);

                    logger.LogUsingKubernetesAuthForVaultServiceAccountTokenPresentApplicationNameAppName(appName);
                    authMethod = new KubernetesAuthMethodInfo(appName, jwt);
                }
                else
                {
                    logger.LogFailedToDetermineVaultAuthenticationMethodNeitherTokenNorKubernetesServiceAccountToken();
                    throw new InvalidOperationException("Failed to determine Vault authentication method");
                }
            }

            IVaultClient client = new VaultClient(new VaultClientSettings(address, authMethod));

            logger.LogCreatedVaultClient(address, authMethod.GetType().Name);
            logger.LogVaultClientCreatedSuccessfullyForAddressAddress(address);
            return client;
        }
        catch (Exception ex)
        {
            logger.LogFailedToCreateVaultClientMessage(ex, ex.Message);
            throw;
        }
    }
}
