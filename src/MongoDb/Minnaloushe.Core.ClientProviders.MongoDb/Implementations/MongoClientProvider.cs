using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.ClientProviders.Abstractions;
using Minnaloushe.Core.ClientProviders.MongoDb.Abstractions;
using Minnaloushe.Core.ClientProviders.MongoDb.Models;
using Minnaloushe.Core.ServiceDiscovery.Abstractions;
using Minnaloushe.Core.Toolbox.AsyncInitializer;
using Minnaloushe.Core.VaultService.Abstractions;
using MongoDB.Driver;
using VaultSharp;

namespace Minnaloushe.Core.ClientProviders.MongoDb.Implementations;

public class MongoClientProvider
    : IMongoClientProvider,
        ICredentialsProvider<MongoDbCredentials>,
        IAsyncInitializer
{
    private readonly IRenewableClientHolder<IMongoClient> _clientHolder;
    private readonly ICredentialsWatcher<MongoDbCredentials> _credentialsWatcher;
    private readonly IClientProvider<IVaultClient> _vaultClientProvider;
    private readonly IVaultCredentialsWatcher _vaultCredentialsWatcher;
    private readonly IApplicationDependenciesProvider _dependenciesProvider;
    private readonly IMongoClientFactory _mongoClientFactory;
    private readonly IOptionsMonitor<RepositoryOptions> _options;
    private readonly string _connectionName;
    private readonly ILogger<MongoClientProvider> _logger;
    private MongoDbCredentials _credentials = null!;

    public MongoClientProvider(
        string connectionName,
        IRenewableClientHolder<IMongoClient> clientHolder,
        IVaultCredentialsWatcher vaultCredentialsWatcher,
        IClientProvider<IVaultClient> vaultClientProvider,
        IApplicationDependenciesProvider dependenciesProvider,
        IMongoClientFactory mongoClientFactory,
        IOptionsMonitor<RepositoryOptions> options,
        ICredentialsWatcher<MongoDbCredentials> credentialsWatcher,
        ILogger<MongoClientProvider> logger
    )
    {
        _connectionName = connectionName;
        _clientHolder = clientHolder;
        _vaultClientProvider = vaultClientProvider;
        _vaultCredentialsWatcher = vaultCredentialsWatcher; // Store for later
        _dependenciesProvider = dependenciesProvider;
        _mongoClientFactory = mongoClientFactory;
        _options = options;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        // DON'T attach here - defer to InitializeAsync
        _credentialsWatcher = credentialsWatcher;

        _logger.Constructed(_connectionName);
    }

    public ClientLease<IMongoClient> Acquire()
    {
        _logger.AcquiringLease(_connectionName);
        return _clientHolder.Acquire();
    }

    public async Task<MongoDbCredentials> IssueAsync(CancellationToken cancellationToken = default)
    {
        _logger.IssuingCredentials(_connectionName);
        try
        {
            using var clientLease = _vaultClientProvider.Acquire();
            _logger.AcquiredVaultClientLease(_connectionName);

            var options = _options.Get(_connectionName);
            _logger.RepositoryOptions(_connectionName, options.ServiceName, options.DatabaseName);

            var role = await _dependenciesProvider.GetDatabaseRole(options.ServiceName);
            _logger.ResolvedDatabaseRole(options.ServiceName, role);

            var credentials = await clientLease.Client.V1.Secrets.Database.GetCredentialsAsync(role);

            _credentials = new MongoDbCredentials(
                Username: credentials.Data.Username,
                Password: credentials.Data.Password,
                LeaseId: credentials.LeaseId,
                LeaseDurationSeconds: credentials.LeaseDurationSeconds,
                Renewable: credentials.Renewable
            );

            // Do not log sensitive values (username/password). Log lease metadata only.
            _logger.ReceivedCredentialsLease(_credentials.LeaseId, _credentials.LeaseDurationSeconds, _credentials.Renewable, _connectionName);

            var client = await _mongoClientFactory.CreateAsync(new MongoConfig(_credentials.Username, _credentials.Password,
                options.ServiceName, options.DatabaseName));

            _clientHolder.RotateClient(client);
            _logger.RotatedClient(_connectionName, options.ServiceName, options.DatabaseName);

            return _credentials;
        }
        catch (Exception ex)
        {
            _logger.IssueFailed(ex, _connectionName, ex.Message);
            throw;
        }
    }

    public async Task<MongoDbCredentials?> RenewAsync(string leaseId, CancellationToken cancellationToken = default)
    {
        _logger.RenewingLease(leaseId, _connectionName);
        try
        {
            using var clientLease = _vaultClientProvider.Acquire();
            var options = _options.Get(_connectionName);
            var leaseDurationInSeconds = options.LeaseRenewInterval.Seconds;

            _logger.RequestingLeaseRenewal(leaseId, leaseDurationInSeconds);
            var newLease = await clientLease.Client.V1.System.RenewLeaseAsync(leaseId, leaseDurationInSeconds);

            if (newLease is not { Renewable: true })
            {
                _logger.LeaseNotRenewable(leaseId, _connectionName);
                return null;
            }

            _logger.LeaseRenewed(leaseId, _connectionName, newLease.LeaseDurationSeconds, newLease.Renewable);

            _credentials = _credentials with
            {
                LeaseDurationSeconds = newLease.LeaseDurationSeconds,
                Renewable = newLease.Renewable
            };

            return _credentials;
        }
        catch (Exception ex)
        {
            _logger.RenewFailed(ex, leaseId, _connectionName, ex.Message);
            throw;
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        _logger.InitializingWatcher(_connectionName);

        // Start our own watcher first
        await _credentialsWatcher.StartAsync(this, cancellationToken);

        // THEN attach to parent - this ensures we don't react to Vault's initial emission
        _credentialsWatcher.AttachToParent(_vaultCredentialsWatcher);

        _logger.StartedWatcher(_connectionName);
    }

    public IObservable<object> CredentialsStream => _credentialsWatcher.CredentialsStream;
}