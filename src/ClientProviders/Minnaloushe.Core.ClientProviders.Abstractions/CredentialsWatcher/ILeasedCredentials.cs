namespace Minnaloushe.Core.ClientProviders.Abstractions.CredentialsWatcher;

public interface ILeasedCredentials
{
    string LeaseId { get; }
    int LeaseDurationSeconds { get; }
    bool Renewable { get; }
}