using Microsoft.Extensions.DependencyInjection;
using Minnaloushe.Core.Toolbox.AsyncInitializer;
using Minnaloushe.Core.Toolbox.AsyncInitializer.Extensions;
using Minnaloushe.Core.VaultOptions.ResolvedOptions;
using Minnaloushe.Core.VaultOptions.Vault;

namespace Minnaloushe.Core.ClientProviders.Abstractions.VaultExtensions;

public static class DependencyRegistration
{
    public static IServiceCollection AddClientProvider<TProvider, TProviderImpl, TOptions>(this IServiceCollection services, string sectionName)
        where TProvider : class, IAsyncInitializer
        where TProviderImpl : class, TProvider
        where TOptions : VaultStoredOptions
    {

        services.AddAsyncInitializer<TProviderImpl>();
        services.AddSingleton<TProvider>(sp => sp.GetRequiredService<TProviderImpl>());

        services.AddOptions<TOptions>()
            .BindConfiguration(sectionName);


        services.AddSingleton<ResolvedOptions<TOptions>>();
        services.AddSingleton(typeof(IResolvedOptions<TOptions>), sp =>
            sp.GetRequiredService(typeof(ResolvedOptions<TOptions>)));

        services.AddAsyncInitializer<VaultOptionsInitializer<TOptions>>();

        return services;
    }
}