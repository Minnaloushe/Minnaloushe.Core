using Microsoft.Extensions.Logging;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Minnaloushe.Core.ClientProviders.Abstractions.CredentialsWatcher;

public class LeasedCredentialWatcher<TCredentials>(ILogger<LeasedCredentialWatcher<TCredentials>> logger)
    : ICredentialsWatcher<TCredentials>, IAsyncDisposable
    where TCredentials : class, ILeasedCredentials
{
    private readonly TimeSpan _minLeaseTime = TimeSpan.FromSeconds(30);
    private readonly CancellationTokenSource _cts = new();
    private readonly Subject<TCredentials> _credentialsChanged = new();
    private IDisposable? _renewalLoop;
    private IDisposable? _parentSubscription;
    private ICredentialsProvider<TCredentials> _provider = null!;
    private int _currentLeaseDurationSeconds;

    public IObservable<object> CredentialsStream => _credentialsChanged.AsObservable();

    /// <summary>
    /// Optionally attach this watcher to another parent watcher.
    /// When parent credentials rotate, this watcher will re-issue its own credentials.
    /// </summary>
    public void AttachToParent<TParent>(ICredentialsWatcher<TParent> parent)
        where TParent : class, ILeasedCredentials
    {
        logger.AttachingToParent(typeof(TParent).Name);

        _parentSubscription = parent.CredentialsStream
            .Sample(TimeSpan.FromSeconds(1))
            .SelectMany(_ =>
                Observable
                    .FromAsync(ForceRefreshAsync)
                    .Catch((Exception ex) =>
            {
                logger.ForcedRefreshTriggeredByParentFailed(ex);
                return Observable.Empty<Unit>();
            }))
            .Subscribe();

        logger.AttachedToParent();
    }

    /// <summary>
    /// Start watching (issue credentials and begin renewal loop).
    /// </summary>
    public async Task StartAsync(ICredentialsProvider<TCredentials> provider, CancellationToken cancellationToken = default)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));

        logger.StartingWatcher(provider.GetType().FullName ?? string.Empty);
        var credentials = await _provider.IssueAsync(cancellationToken);
        logger.InitialCredentialsIssued(credentials.LeaseId, credentials.LeaseDurationSeconds, credentials.Renewable);

        _credentialsChanged.OnNext(credentials);
        StartRenewalLoop(credentials);
    }

    //TODO Review and replace usages to StartAsync
    public async Task ForceRefreshAsync()
    {
        logger.ForceRefreshInvoked();
        var credentials = await _provider.IssueAsync(_cts.Token);
        logger.CredentialsReissuedForced(credentials.LeaseId, credentials.LeaseDurationSeconds, credentials.Renewable);

        _credentialsChanged.OnNext(credentials);
        StartRenewalLoop(credentials);
    }

    private void StartRenewalLoop(TCredentials credentials)
    {
        var leaseDuration = TimeSpan.FromSeconds(credentials.LeaseDurationSeconds);
        var renewDelay = TimeSpan.FromTicks(leaseDuration.Ticks * 2 / 3);
        if (renewDelay < _minLeaseTime)
        {
            renewDelay = _minLeaseTime;
        }

        // Only recreate the renewal loop if lease duration has changed
        if (credentials.LeaseDurationSeconds == _currentLeaseDurationSeconds)
        {
            logger.LeaseDurationUnchanged(credentials.LeaseDurationSeconds, credentials.LeaseId);
            return;
        }

        _currentLeaseDurationSeconds = credentials.LeaseDurationSeconds;

        logger.StartingRenewalLoop(credentials.LeaseId, leaseDuration, renewDelay);

        _renewalLoop?.Dispose();

        _renewalLoop = Observable
            .Timer(renewDelay, renewDelay) // Initial delay, then repeat at same interval
            .SelectMany(_ =>
                Observable.FromAsync(async () =>
                    {
                        var (updated, reissued) = await TryRenewOrReissueAsync(credentials);

                        // Emit to subscribers only when credentials were reissued (not on successful renew)
                        if (reissued)
                        {
                            _credentialsChanged.OnNext(updated);
                        }

                        // Update credentials reference for next iteration without restarting the loop
                        credentials = updated;
                        return updated;
                    }
                ))
            .TakeUntil(_ => _cts.IsCancellationRequested)
            .Subscribe(
                onNext: _ => { },
                onError: logger.RenewalLoopFailed,
                onCompleted: logger.RenewalLoopCompleted
            );
    }

    /// <summary>
    /// Attempts to renew; if renew fails or returns null, issues new credentials.
    /// Returns tuple: (resulting credentials, whether it was a reissue (true) or a renew (false)).
    /// </summary>
    private async Task<(TCredentials Result, bool Reissued)> TryRenewOrReissueAsync(TCredentials current)
    {
        logger.AttemptingRenewOrReissue(current.LeaseId, current.Renewable);
        try
        {
            TCredentials? renewed = null;
            //TODO Add readiness probe status Degraded when reissuing fails
            if (current.Renewable)
            {
                logger.CredentialsAreRenewableAttemptRenew(current.LeaseId);
                try
                {
                    renewed = await _provider.RenewAsync(current.LeaseId, _cts.Token);
                    if (renewed != null)
                    {
                        logger.SuccessfullyRenewedLease(current.LeaseId, renewed.LeaseDurationSeconds, renewed.Renewable);
                        return (renewed, false);
                    }
                }
                catch (Exception ex)
                {
                    logger.RenewAsyncFailed(ex, current.LeaseId, ex.Message);
                }
            }

            var result = renewed ?? await _provider.IssueAsync(_cts.Token);

            logger.IssuedNewCredentials(result.LeaseId);

            return (result, true);
        }
        catch (Exception ex)
        {
            logger.FailedToRenewOrReissue(ex, current.LeaseId, ex.Message);
            var fallback = await _provider.IssueAsync(_cts.Token);
            return (fallback, true);
        }
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        logger.Disposing();
        await _cts.CancelAsync();
        _renewalLoop?.Dispose();
        _parentSubscription?.Dispose();
        _credentialsChanged.Dispose();
        logger.Disposed();

        GC.SuppressFinalize(this);
    }
}