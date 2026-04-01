using Microsoft.Extensions.Logging;
using Minnaloushe.Core.ClientProviders.Abstractions;
using Minnaloushe.Core.ClientProviders.Abstractions.CredentialsWatcher;
using Minnaloushe.Core.Toolbox.AsyncInitializer;
using Minnaloushe.Core.VaultService.CredentialsWatcher;
using Minnaloushe.Core.VaultService.Factory;
using Minnaloushe.Core.VaultService.Implementations;
using System.Diagnostics;
using VaultSharp;

namespace Minnaloushe.Core.VaultService.Adapter;

internal class VaultClientAdapter
    : ICredentialsProvider<VaultClientCredentials>,
        IVaultCredentialsWatcher,
        IClientProvider<IVaultClient>,
        IAsyncInitializer,
        IAsyncDisposable
{
    private readonly ICredentialsWatcher<VaultClientCredentials> _watcher;

    private readonly IRenewableClientHolder<IVaultClient> _clientHolder;
    private readonly IVaultClientFactory _clientFactory;
    private readonly ILogger<VaultClientAdapter> _logger;

    public VaultClientAdapter(
        IRenewableClientHolder<IVaultClient> clientHolder,
        IVaultClientFactory clientFactory,
        ICredentialsWatcher<VaultClientCredentials> watcher,
        ILogger<VaultClientAdapter> logger
    )
    {
        _clientHolder = clientHolder;
        _clientFactory = clientFactory;
        _logger = logger;
        _watcher = watcher;

        logger.LogVaultClientAdapterConstructed();
    }
    public async Task<VaultClientCredentials> IssueAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogIssuingVaultClientCredentialsStartingCreateAsync();
        try
        {
            var sw = Stopwatch.StartNew();
            var client = await _clientFactory.CreateAsync(null, cancellationToken);
            sw.Stop();
            _logger.LogVaultClientCreationElapsed(sw.Elapsed.TotalMilliseconds);

            _logger.LogVaultClientInstanceCreatedPerformingTokenSelfLookup();

            var swLookup = Stopwatch.StartNew();
            var selfAuth = await client.V1.Auth.Token.LookupSelfAsync();
            swLookup.Stop();
            _logger.LogVaultTokenLookupElapsed(swLookup.Elapsed.TotalMilliseconds);

            if (selfAuth == null || selfAuth.Data == null)
            {
                _logger.LogLookupSelfAsyncReturnedNullCannotObtainTokenInfo();
                throw new InvalidOperationException("Failed to lookup Vault token info");
            }

            _clientHolder.RotateClient(client);

            // For tokens, use the token ID as the "lease ID" and get TTL from Data
            var tokenId = selfAuth.Data.Id ?? string.Empty;
            var ttl = selfAuth.Data.TimeToLive;
            var renewable = selfAuth.Data.Renewable;

            _logger.LogIssuedVaultClientCredentials(tokenId, ttl, renewable);

            return new VaultClientCredentials(
                LeaseId: tokenId,
                LeaseDurationSeconds: ttl,
                Renewable: renewable);
        }
        catch (Exception ex)
        {
            _logger.LogFailedToIssueVaultClientCredentialsMessage(ex, ex.Message);
            throw;
        }
    }

    public async Task<VaultClientCredentials?> RenewAsync(string leaseId, CancellationToken cancellationToken = default)
    {
        _logger.LogRenewingVaultTokenLeaseLeaseId(leaseId);

        try
        {
            using var clientLease = _clientHolder.Acquire();

            var renewResult = await clientLease.Client.V1.Auth.Token.RenewSelfAsync();
            if (renewResult == null)
            {
                _logger.LogTokenRenewReturnedNullForLeaseLeaseId(leaseId);
                return null;
            }

            _logger.LogRenewedVaultTokenLease(leaseId, renewResult.LeaseDurationSeconds, renewResult.Renewable);

            return new VaultClientCredentials(
                LeaseId: leaseId,
                LeaseDurationSeconds: renewResult.LeaseDurationSeconds,
                Renewable: renewResult.Renewable);
        }
        catch (Exception ex)
        {
            _logger.LogFailedToRenewVaultTokenLease(ex, leaseId, ex.Message);
            throw;
        }
    }

    public async Task<bool> InitializeAsync(CancellationToken cancellationToken)
    {
        _logger.LogInitializingVaultClientAdapterWatcher();
        await StartAsync(this, cancellationToken);
        _logger.LogVaultClientAdapterWatcherStarted();

        return true;
    }

    public async ValueTask DisposeAsync()
    {
        _logger.LogDisposingVaultClientAdapterWatcher();
        if (_watcher is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        _logger.LogVaultClientAdapterDisposed();
    }

    public ClientProviders.Abstractions.ClientLease.IClientLease<IVaultClient> Acquire()
    {
        _logger.LogAcquiringClientLeaseFromHolder();
        return _clientHolder.Acquire();
    }

    public void AttachToParent<TParent>(ICredentialsWatcher<TParent> parent) where TParent : class, ILeasedCredentials
    {
        _logger.LogAttachingVaultClientAdapterWatcherToParentWatcher(typeof(TParent).Name);
        _watcher.AttachToParent(parent);
    }

    public IObservable<object> CredentialsStream => _watcher.CredentialsStream;

    public Task StartAsync(ICredentialsProvider<VaultClientCredentials> provider,
        CancellationToken cancellationToken = default) => _watcher.StartAsync(provider, cancellationToken);
}