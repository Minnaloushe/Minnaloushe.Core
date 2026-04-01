using Microsoft.Extensions.DependencyInjection;
using Minnaloushe.Core.VaultOptions.Vault;

namespace Minnaloushe.Core.VaultOptions.Extensions;

public static class DependencyRegistration
{
    public static IServiceCollection AddVaultStoredOptions(
        this IServiceCollection services)
    {
        services.AddSingleton(typeof(IVaultOptionsLoader<>), typeof(VaultOptionsLoader<>));

        return services;
    }
}