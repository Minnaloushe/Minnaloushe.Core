using Minnaloushe.Core.ClientProviders.Abstractions.CredentialsWatcher;

namespace Minnaloushe.Core.VaultService.CredentialsWatcher;

public interface IVaultCredentialsWatcher : ICredentialsWatcher<VaultClientCredentials>
{
}