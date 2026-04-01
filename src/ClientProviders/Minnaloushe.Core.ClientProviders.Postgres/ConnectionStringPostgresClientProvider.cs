using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.ClientProviders.Abstractions.ClientLease;
using Minnaloushe.Core.ClientProviders.Abstractions.CredentialsWatcher;
using Minnaloushe.Core.ClientProviders.Postgres.Implementations;
using Minnaloushe.Core.Repositories.Abstractions;
using Minnaloushe.Core.Toolbox.AsyncInitializer;
using Npgsql;

namespace Minnaloushe.Core.ClientProviders.Postgres;/// <summary>
/// PostgreSQL client provider that uses a static connection string from configuration.
/// Does not involve Vault or credential rotation.
/// </summary>
public class ConnectionStringPostgresClientProvider : IPostgresClientProvider, IAsyncInitializer
{
    private readonly IRenewableClientHolder<NpgsqlDataSource> _clientHolder;
    private readonly IOptionsMonitor<RepositoryOptions> _options;
    private readonly string _connectionName;
    private readonly ILogger<ConnectionStringPostgresClientProvider> _logger;

    public ConnectionStringPostgresClientProvider(
        string connectionName,
        IRenewableClientHolder<NpgsqlDataSource> clientHolder,
        IOptionsMonitor<RepositoryOptions> options,
        ILogger<ConnectionStringPostgresClientProvider> logger)
    {
        _connectionName = connectionName;
        _clientHolder = clientHolder;
        _options = options;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.Constructed(_connectionName);
    }

    public IClientLease<NpgsqlConnection> Acquire()
    {
        return TransactionAcquisitionHelper.Acquire(_clientHolder, _connectionName);
    }

    public Task<bool> InitializeAsync(CancellationToken cancellationToken)
    {
        _logger.Initializing(_connectionName);

        var options = _options.Get(_connectionName);

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            _logger.LogWarning($"ConnectionString must be provided for connection '{_connectionName}' when using ConnectionStringPostgresClientProvider.");
            return Task.FromResult(false);
        }

        _logger.CreatingConnection(_connectionName);

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(options.ConnectionString);
        var dataSource = dataSourceBuilder.Build();
        _clientHolder.RotateClient(dataSource);

        _logger.Initialized(_connectionName);

        return Task.FromResult(true);
    }

    public IObservable<object> CredentialsStream => System.Reactive.Linq.Observable.Empty<object>();
}