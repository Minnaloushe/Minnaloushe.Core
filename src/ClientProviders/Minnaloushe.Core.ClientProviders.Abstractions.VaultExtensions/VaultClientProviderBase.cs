using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.ClientProviders.Abstractions.CredentialsWatcher;
using Minnaloushe.Core.ClientProviders.Abstractions.Factories;
using Minnaloushe.Core.Toolbox.AsyncInitializer;
using Minnaloushe.Core.VaultService.CredentialsWatcher;
using VaultSharp;
using VaultSharp.V1.Commons;
using VaultSharp.V1.SecretsEngines;

namespace Minnaloushe.Core.ClientProviders.Abstractions.VaultExtensions;

public abstract class VaultClientProviderBase<TClient, TCredentials, TConfig>(
string connectionName,
IRenewableClientHolder<TClient> clientHolder,
IVaultCredentialsWatcher vaultCredentialsWatcher,
IClientProvider<IVaultClient> vaultClientProvider,
IClientFactory<TClient, TConfig> clientFactory,
IOptionsMonitor<VaultClientOptions> optionsMonitor,
ICredentialsWatcher<TCredentials> credentialsWatcher,
Func<string, Task<string>> getDatabaseRoleFunc,
ILogger logger)
: IClientProvider<TClient>,
    ICredentialsProvider<TCredentials>,
    IAsyncInitializer
where TClient : class
where TCredentials : LeasedCredentials
{
    private TCredentials _credentials = null!;

    public ClientLease.IClientLease<TClient> Acquire()
    {
        logger.AcquiringLease(connectionName);
        return clientHolder.Acquire();
    }

    protected abstract TCredentials CreateCredentials(Secret<UsernamePasswordCredentials> vaultCredentials);
    protected abstract TConfig CreateConfig(TCredentials credentials, VaultClientOptions options);

    public async Task<TCredentials> IssueAsync(CancellationToken cancellationToken = default)
    {
        using var loggerScope = logger.BeginScope("Issuing credentials for connection {ConnectionName}", connectionName);

        logger.IssuingCredentials(connectionName);
        try
        {
            using var clientLease = vaultClientProvider.Acquire();
            logger.AcquiredVaultClientLease(connectionName);

            var options = optionsMonitor.Get(connectionName);
            logger.RepositoryOptions(connectionName, options.ServiceName, options.DatabaseName);

            var role = await getDatabaseRoleFunc(options.ServiceName);
            logger.ResolvedDatabaseRole(options.ServiceName, role);

            var credentials = await clientLease.Client.V1.Secrets.Database.GetCredentialsAsync(role);

            _credentials = CreateCredentials(credentials);
            logger.ReceivedCredentialsLease(_credentials.LeaseId, _credentials.LeaseDurationSeconds, _credentials.Renewable, connectionName);

            var client = await clientFactory.CreateAsync(
                    CreateConfig(_credentials,
                    options),
                cancellationToken
                );

            clientHolder.RotateClient(client);
            logger.RotatedClient(connectionName, options.ServiceName, options.DatabaseName);

            return _credentials;
        }
        catch (Exception ex)
        {
            logger.IssueFailed(ex, connectionName, ex.Message);
            throw;
        }
    }

    public async Task<TCredentials?> RenewAsync(string leaseId, CancellationToken cancellationToken = default)
    {
        using var loggerScope = logger.BeginScope("Renewing credentials for connection {ConnectionName}", connectionName);

        logger.RenewingLease(leaseId, connectionName);
        try
        {
            using var clientLease = vaultClientProvider.Acquire();
            var repositoryOptions = optionsMonitor.Get(connectionName);
            var leaseDurationInSeconds = repositoryOptions.LeaseRenewInterval.Seconds;

            logger.RequestingLeaseRenewal(leaseId, leaseDurationInSeconds);
            var newLease = await clientLease.Client.V1.System.RenewLeaseAsync(leaseId, leaseDurationInSeconds);

            if (newLease is not { Renewable: true })
            {
                logger.LeaseNotRenewable(leaseId, connectionName);
                return null;
            }

            logger.LeaseRenewed(leaseId, connectionName, newLease.LeaseDurationSeconds, newLease.Renewable);

            _credentials = _credentials
                with
            {
                LeaseDurationSeconds = newLease.LeaseDurationSeconds,
                Renewable = newLease.Renewable
            };

            return _credentials;
        }
        catch (Exception ex)
        {
            logger.RenewFailed(ex, leaseId, connectionName, ex.Message);
            throw;
        }
    }

    public async Task<bool> InitializeAsync(CancellationToken cancellationToken)
    {
        logger.InitializingWatcher(connectionName);

        await credentialsWatcher.StartAsync(this, cancellationToken);
        credentialsWatcher.AttachToParent(vaultCredentialsWatcher);

        logger.StartedWatcher(connectionName);

        return true;
    }

    public IObservable<object> CredentialsStream => credentialsWatcher.CredentialsStream;
}