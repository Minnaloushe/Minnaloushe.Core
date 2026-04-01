using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.ClientProviders.Abstractions.VaultExtensions;
using Minnaloushe.Core.ClientProviders.Postgres.Extensions;
using Minnaloushe.Core.ClientProviders.Postgres.Factories;
using Minnaloushe.Core.Repositories.Abstractions;
using Minnaloushe.Core.Repositories.DependencyInjection.Models;
using Minnaloushe.Core.Repositories.Postgres.Factories;
using Minnaloushe.Core.Repositories.Postgres.Vault.Factories;

namespace Minnaloushe.Core.Repositories.Postgres.Vault.Extensions;

/// <summary>
/// Vault-specific PostgreSQL repository configuration extensions.
/// </summary>
public static class VaultPostgresClientProviderExtensions
{
    /// <summary>
    /// Adds Vault-based PostgreSQL client provider factory.
    /// Call this after AddPostgresDbClientProviders() and before Build().
    /// </summary>
    public static RepositoryBuilder AddVaultPostgresDbClientProviders(this RepositoryBuilder builder)
    {
        builder.Services.AddPostgresRegistration();

        builder.Services.AddSingleton<IPostgresClientFactory, PostgresClientFactory>();
        builder.Services.AddSingleton<IPostgresClientProviderFactory, VaultPostgresClientProviderFactory>();

        ConfigureVaultClientOptions(builder);

        return builder;
    }

    private static void ConfigureVaultClientOptions(RepositoryBuilder builder)
    {
        var repoConfigSection = builder.Configuration.GetSection("RepositoryConfiguration");
        var repositories = repoConfigSection.GetSection("Repositories").Get<List<RepositoryDefinition>>() ?? [];
        var connectionSections = repoConfigSection.GetSection("Connections").GetChildren()
            .ToDictionary(s => s.GetValue<string>(nameof(ConnectionDefinition.Name))!, StringComparer.OrdinalIgnoreCase);

        var distinctConnectionNames = repositories
            .Select(r => r.ConnectionName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var connectionName in distinctConnectionNames)
        {
            if (!connectionSections.TryGetValue(connectionName, out var connSection))
            {
                continue;
            }

            var type = connSection.GetValue<string>(nameof(ConnectionDefinition.Type));
            if (type is not ("postgres" or "postgresql"))
            {
                continue;
            }

            builder.Services.AddOptions<VaultClientOptions>(connectionName)
                .Configure<IOptionsMonitor<RepositoryOptions>>((vaultOptions, repoOptionsMonitor) =>
                {
                    var repoOptions = repoOptionsMonitor.Get(connectionName);
                    vaultOptions.ServiceName = repoOptions.ServiceName;
                    vaultOptions.DatabaseName = repoOptions.DatabaseName;
                    vaultOptions.LeaseRenewInterval = repoOptions.LeaseRenewInterval;
                });
        }
    }
}
