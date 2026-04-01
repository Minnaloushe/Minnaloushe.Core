using Minnaloushe.Core.ClientProviders.Abstractions.Factories;
using VaultSharp;

namespace Minnaloushe.Core.VaultService.Factory;

public interface IVaultClientFactory : IClientFactory<IVaultClient, object>
{
}