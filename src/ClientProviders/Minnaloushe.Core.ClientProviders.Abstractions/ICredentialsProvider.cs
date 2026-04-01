using Minnaloushe.Core.ClientProviders.Abstractions.CredentialsWatcher;

namespace Minnaloushe.Core.ClientProviders.Abstractions;

public interface ICredentialsProvider<TCredentials> where TCredentials : class, ILeasedCredentials
{
    /// <summary>Request a new credential lease (initial issue or after expiry).</summary>
    Task<TCredentials> IssueAsync(CancellationToken cancellationToken = default);

    /// <summary>Attempt to renew an existing lease. Returns null if renewal fails.</summary>
    Task<TCredentials?> RenewAsync(string leaseId, CancellationToken cancellationToken = default);
}