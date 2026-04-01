namespace Minnaloushe.Core.ClientProviders.Abstractions.CredentialsWatcher;

public interface IObservableCredentialsWatcher
{
    IObservable<object> CredentialsStream { get; }
}