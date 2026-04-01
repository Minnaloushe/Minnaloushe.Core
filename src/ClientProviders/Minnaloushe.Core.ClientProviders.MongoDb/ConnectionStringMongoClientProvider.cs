using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.ClientProviders.Abstractions.ClientLease;
using Minnaloushe.Core.ClientProviders.Abstractions.CredentialsWatcher;
using Minnaloushe.Core.ClientProviders.MongoDb.Implementations;
using Minnaloushe.Core.Repositories.Abstractions;
using Minnaloushe.Core.Toolbox.AsyncInitializer;
using MongoDB.Driver;

namespace Minnaloushe.Core.ClientProviders.MongoDb;

/// <summary>
/// MongoDB client provider that uses a static connection string from configuration.
/// Does not involve Vault or credential rotation.
/// </summary>
public class ConnectionStringMongoClientProvider : IMongoClientProvider, IAsyncInitializer
{
    private readonly IRenewableClientHolder<IMongoClient> _clientHolder;
    private readonly IOptionsMonitor<RepositoryOptions> _options;
    private readonly string _connectionName;
    private readonly ILogger<ConnectionStringMongoClientProvider> _logger;

    public ConnectionStringMongoClientProvider(
        string connectionName,
        IRenewableClientHolder<IMongoClient> clientHolder,
        IOptionsMonitor<RepositoryOptions> options,
        ILogger<ConnectionStringMongoClientProvider> logger)
    {
        _connectionName = connectionName;
        _clientHolder = clientHolder;
        _options = options;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.Constructed(_connectionName);
    }

    public IClientLease<IMongoClient> Acquire()
    {
        _logger.AcquiringLease(_connectionName);
        return _clientHolder.Acquire();
    }

    public Task<bool> InitializeAsync(CancellationToken cancellationToken)
    {
        _logger.Initializing(_connectionName);

        var options = _options.Get(_connectionName);

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            _logger.MissingConnectionString(_connectionName);
            return Task.FromResult(false);
        }

        _logger.CreatingClient(_connectionName);

        // Create client directly from connection string
        var client = new MongoClient(options.ConnectionString);

        // Initialize holder with client
        _clientHolder.RotateClient(client);

        _logger.Initialized(_connectionName);

        return Task.FromResult(true);
    }

    public IObservable<object> CredentialsStream => System.Reactive.Linq.Observable.Empty<object>();
}

