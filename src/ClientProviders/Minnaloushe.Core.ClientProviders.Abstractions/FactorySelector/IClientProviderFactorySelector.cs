using Minnaloushe.Core.ClientProviders.Abstractions.Factories;

namespace Minnaloushe.Core.ClientProviders.Abstractions.FactorySelector;

public interface IClientProviderFactorySelector<TProvider, TFactory, in TOptions>
    where TFactory : IClientProviderFactory<TProvider, TOptions>
{
    /// <summary>
    /// Selects the first factory that can handle the given options.
    /// </summary>
    /// <param name="options">The repository options.</param>
    /// <param name="factories">The available factories.</param>
    /// <returns>The selected factory, or null if no factory can handle the options.</returns>
    TFactory? SelectFactory(TOptions options, IEnumerable<TFactory> factories);
}