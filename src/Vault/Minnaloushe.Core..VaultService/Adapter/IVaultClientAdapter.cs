using VaultSharp.V1.AuthMethods.Token.Models;
using VaultSharp.V1.Commons;
using VaultSharp.V1.SecretsEngines;
using VaultSharp.V1.SystemBackend;

namespace Minnaloushe.Core.VaultService.Adapter;

public interface IVaultClientAdapter
{
    Task<AuthInfo> RenewSelfAsync();
    Task<Secret<CallingTokenInfo>> LookupSelfAsync();
    Task<Secret<Dictionary<string, object>>> ReadSecretAsync(string name);
    Task<Secret<UsernamePasswordCredentials>> GetCredentialsAsync(string role);
    Task<Secret<RenewedLease>> RenewLeaseAsync(string leaseId, int leaseDurationInSeconds);

    Task<Secret<Dictionary<string, object>>> WriteSecretAsync(string secretName, IDictionary<string, object> expected,
        string mountPoint);
}