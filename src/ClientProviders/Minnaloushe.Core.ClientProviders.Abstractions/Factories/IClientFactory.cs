namespace Minnaloushe.Core.ClientProviders.Abstractions.Factories;

public interface IClientFactory<TClient, in TConfig> where TClient : class
{
    Task<TClient> CreateAsync(TConfig? config, CancellationToken cancellationToken);
}