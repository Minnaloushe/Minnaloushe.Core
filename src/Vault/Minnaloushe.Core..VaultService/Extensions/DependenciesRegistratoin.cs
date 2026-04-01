using Microsoft.Extensions.DependencyInjection;
using Minnaloushe.Core.ClientProviders.Abstractions;
using Minnaloushe.Core.ClientProviders.Abstractions.CredentialsWatcher;
using Minnaloushe.Core.Toolbox.AsyncInitializer;
using Minnaloushe.Core.VaultService.Adapter;
using Minnaloushe.Core.VaultService.CredentialsWatcher;
using Minnaloushe.Core.VaultService.Factory;
using Minnaloushe.Core.VaultService.Options;
using VaultSharp;

namespace Minnaloushe.Core.VaultService.Extensions;

public static class DependencyRegistration
{
    public static IServiceCollection AddVaultClientProvider(this IServiceCollection services)
    {
        services
            .AddTransient<ICredentialsWatcher<VaultClientCredentials>,
                LeasedCredentialWatcher<VaultClientCredentials>>();
        services.AddSingleton<IRenewableClientHolder<IVaultClient>, RenewableClientHolder<IVaultClient>>();
        services.AddSingleton<IVaultClientFactory, VaultClientFactory>();
        services.AddSingleton<VaultClientAdapter>();
        services.AddSingleton<IClientProvider<IVaultClient>>(sp => sp.GetRequiredService<VaultClientAdapter>());
        services.AddSingleton<IVaultCredentialsWatcher>(sp => sp.GetRequiredService<VaultClientAdapter>());
        services.AddSingleton<IAsyncInitializer>(sp => sp.GetRequiredService<VaultClientAdapter>());

        services.AddOptions<VaultOptions>()
            .BindConfiguration(VaultOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}