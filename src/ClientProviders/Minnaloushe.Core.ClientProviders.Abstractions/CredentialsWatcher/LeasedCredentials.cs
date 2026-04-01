namespace Minnaloushe.Core.ClientProviders.Abstractions.CredentialsWatcher;

public record LeasedCredentials : ILeasedCredentials
{
    public string LeaseId { get; init; } = string.Empty;
    public int LeaseDurationSeconds { get; init; }
    public bool Renewable { get; init; }
}