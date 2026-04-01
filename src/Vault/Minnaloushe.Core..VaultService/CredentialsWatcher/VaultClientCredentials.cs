using Minnaloushe.Core.ClientProviders.Abstractions.CredentialsWatcher;

namespace Minnaloushe.Core.VaultService.CredentialsWatcher;

public record VaultClientCredentials(string LeaseId, int LeaseDurationSeconds, bool Renewable) : ILeasedCredentials
{

}