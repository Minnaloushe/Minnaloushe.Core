using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.ClientProviders.Abstractions;
using Minnaloushe.Core.ClientProviders.Abstractions.ClientLease;
using Minnaloushe.Core.ClientProviders.Abstractions.CredentialsWatcher;
using Minnaloushe.Core.ClientProviders.Abstractions.VaultExtensions;
using Minnaloushe.Core.ClientProviders.Postgres.Factories;
using Minnaloushe.Core.ClientProviders.Postgres.Models;
using Minnaloushe.Core.VaultService.CredentialsWatcher;
using Npgsql;
using VaultSharp;
using VaultSharp.V1.Commons;
using VaultSharp.V1.SecretsEngines;

namespace Minnaloushe.Core.ClientProviders.Postgres.Vault;

/// <summary>
/// Vault-based PostgreSQL client provider with dynamic credential management.
/// </summary>
public class PostgresClientProvider(
    string connectionName,
    IRenewableClientHolder<NpgsqlDataSource> clientHolder,
    IVaultCredentialsWatcher vaultCredentialsWatcher,
    IClientProvider<IVaultClient> vaultClientProvider,
    IPostgresClientFactory postgresClientFactory,
    IOptionsMonitor<VaultClientOptions> optionsMonitor,
    ICredentialsWatcher<PostgresCredentials> credentialsWatcher,
    Func<string, Task<string>> getDatabaseRoleFunc,
    ILogger<PostgresClientProvider> logger)
    : VaultClientProviderBase<NpgsqlDataSource, PostgresCredentials, PostgresConfig>(
            connectionName,
            clientHolder,
            vaultCredentialsWatcher,
            vaultClientProvider,
            postgresClientFactory,
            optionsMonitor,
            credentialsWatcher,
            getDatabaseRoleFunc,
            logger),
        IPostgresClientProvider
{
    private readonly string _connectionName = connectionName;
    private readonly IRenewableClientHolder<NpgsqlDataSource> _clientHolder = clientHolder;

    protected override PostgresCredentials CreateCredentials(Secret<UsernamePasswordCredentials> vaultCredentials)
    {
        return new PostgresCredentials(
            Username: vaultCredentials.Data.Username,
            Password: vaultCredentials.Data.Password
        )
        {
            LeaseId = vaultCredentials.LeaseId,
            LeaseDurationSeconds = vaultCredentials.LeaseDurationSeconds,
            Renewable = vaultCredentials.Renewable
        };
    }

    protected override PostgresConfig CreateConfig(PostgresCredentials credentials, VaultClientOptions options)
    {
        return new PostgresConfig(
            Username: credentials.Username,
            Password: credentials.Password,
            ServiceName: options.ServiceName,
            Database: options.DatabaseName);
    }

    IClientLease<NpgsqlConnection> IClientProvider<NpgsqlConnection>.Acquire()
    {
        return TransactionAcquisitionHelper.Acquire(_clientHolder, _connectionName);
    }
}