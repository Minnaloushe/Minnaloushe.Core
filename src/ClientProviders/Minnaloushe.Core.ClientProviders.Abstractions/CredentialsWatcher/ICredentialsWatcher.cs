namespace Minnaloushe.Core.ClientProviders.Abstractions.CredentialsWatcher;

public interface ICredentialsWatcher<TCredentials> : IObservableCredentialsWatcher where TCredentials : class, ILeasedCredentials
{
    /// <summary>Stream of current valid credentials.</summary>
    void AttachToParent<TParent>(ICredentialsWatcher<TParent> parent)
        where TParent : class, ILeasedCredentials;
    Task StartAsync(ICredentialsProvider<TCredentials> provider, CancellationToken cancellationToken = default);
}