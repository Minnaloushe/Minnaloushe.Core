using Minnaloushe.Core.ClientProviders.Abstractions.Factories;

namespace Minnaloushe.Core.ClientProviders.Abstractions.FactorySelector;

public class DefaultClientProviderFactorySelector<TProvider, TFactory, TOptions> : IClientProviderFactorySelector<TProvider, TFactory, TOptions>
    where TFactory : IClientProviderFactory<TProvider, TOptions>
{
    public TFactory? SelectFactory(TOptions options, IEnumerable<TFactory> factories)
    {
        return factories.FirstOrDefault(f => f.CanCreate(options));
    }
}