using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.ClientProviders.Abstractions;
using Minnaloushe.Core.ClientProviders.Abstractions.CredentialsWatcher;
using Minnaloushe.Core.ClientProviders.Abstractions.VaultExtensions;
using Minnaloushe.Core.ClientProviders.MongoDb.Factories;
using Minnaloushe.Core.ClientProviders.MongoDb.Models;
using Minnaloushe.Core.VaultService.CredentialsWatcher;
using MongoDB.Driver;
using VaultSharp;
using VaultSharp.V1.Commons;
using VaultSharp.V1.SecretsEngines;

namespace Minnaloushe.Core.ClientProviders.MongoDb.Vault;

public class MongoClientProvider(
string connectionName,
IRenewableClientHolder<IMongoClient> clientHolder,
IVaultCredentialsWatcher vaultCredentialsWatcher,
IClientProvider<IVaultClient> vaultClientProvider,
IMongoClientFactory mongoClientFactory,
IOptionsMonitor<VaultClientOptions> optionsMonitor,
ICredentialsWatcher<MongoDbCredentials> credentialsWatcher,
Func<string, Task<string>> getDatabaseRoleFunc,
ILogger<MongoClientProvider> logger)
: VaultClientProviderBase<IMongoClient, MongoDbCredentials, MongoConfig>(
    connectionName,
    clientHolder,
    vaultCredentialsWatcher,
    vaultClientProvider,
    mongoClientFactory,
    optionsMonitor,
    credentialsWatcher,
    getDatabaseRoleFunc,
    logger),
    IMongoClientProvider
{
    protected override MongoDbCredentials CreateCredentials(Secret<UsernamePasswordCredentials> vaultCredentials)
    {
        return new MongoDbCredentials(
            Username: vaultCredentials.Data.Username,
            Password: vaultCredentials.Data.Password)
        {
            LeaseId = vaultCredentials.LeaseId,
            LeaseDurationSeconds = vaultCredentials.LeaseDurationSeconds,
            Renewable = vaultCredentials.Renewable
        };
    }

    protected override MongoConfig CreateConfig(MongoDbCredentials credentials, VaultClientOptions options)
    {
        return new MongoConfig(
            credentials.Username,
            credentials.Password,
            options.ServiceName,
            options.DatabaseName);
    }
}
