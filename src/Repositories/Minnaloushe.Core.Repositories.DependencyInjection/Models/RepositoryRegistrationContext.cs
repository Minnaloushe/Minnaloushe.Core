using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.ClientProviders.Abstractions.Factories;
using Minnaloushe.Core.ClientProviders.Abstractions.FactorySelector;
using Minnaloushe.Core.Repositories.Abstractions;
using Minnaloushe.Core.Toolbox.AsyncInitializer;

namespace Minnaloushe.Core.Repositories.DependencyInjection.Models;

/// <summary>
/// Context information passed to repository handlers during registration.
/// </summary>
/// <param name="Services">The service collection to register services into.</param>
/// <param name="ConnectionName">The name of the connection being registered.</param>
/// <param name="ConnectionSection">The configuration section for this connection.</param>
/// <param name="Repositories">The list of repositories using this connection.</param>
public sealed record RepositoryRegistrationContext(
    IServiceCollection Services,
    string ConnectionName,
    IConfigurationSection ConnectionSection,
    List<RepositoryDefinition> Repositories)
{
    /// <summary>
    /// Registers a keyed client provider using factory selection pattern.
    /// This is the common registration logic shared by MongoDB, PostgreSQL, and other providers.
    /// </summary>
    /// <typeparam name="TProvider">The provider interface type.</typeparam>
    /// <typeparam name="TFactory">The factory interface type.</typeparam>
    /// <typeparam name="TSelector">The factory selector interface type.</typeparam>
    public void RegisterKeyedProvider<TProvider, TFactory, TSelector>()
        where TProvider : class
        where TFactory : IClientProviderFactory<TProvider, RepositoryOptions>
        where TSelector : class, IClientProviderFactorySelector<TProvider, TFactory, RepositoryOptions>
    {
        // Register the provider with deferred factory selection
        Services.AddKeyedSingleton<TProvider>(ConnectionName, (sp, key) =>
        {
            var options = sp.GetRequiredService<IOptionsMonitor<RepositoryOptions>>().Get(ConnectionName);
            var factories = sp.GetServices<TFactory>();
            var selector = sp.GetRequiredService<TSelector>();

            var factory = selector.SelectFactory(options, factories);

            return factory == null
                ? throw new InvalidOperationException(
                    $"No registered {typeof(TFactory).Name} can handle connection '{ConnectionName}'. " +
                    $"ServiceName='{options.ServiceName}', ConnectionString='{(string.IsNullOrWhiteSpace(options.ConnectionString) ? "(empty)" : "(set)")}'")
                : factory.Create(ConnectionName);
        });

        // Register as IAsyncInitializer for initialization
        Services.AddSingleton<IAsyncInitializer>(sp =>
        {
            var provider = sp.GetRequiredKeyedService<TProvider>(ConnectionName);
            return provider is not IAsyncInitializer initializer
                ? throw new InvalidOperationException(
                    $"Provider for connection '{ConnectionName}' does not implement IAsyncInitializer.")
                : initializer;
        });

        // Register keyed providers for all repositories using this connection
        foreach (var repo in Repositories)
        {
            Services.AddKeyedSingleton<TProvider>(repo.Name,
                (sp, key) => sp.GetRequiredKeyedService<TProvider>(ConnectionName));
        }
    }
}

/// <summary>
/// Definition of a repository from configuration.
/// </summary>
public sealed record RepositoryDefinition
{
    public string Name { get; init; } = string.Empty;
    public string ConnectionName { get; init; } = string.Empty;
}

/// <summary>
/// Definition of a connection from configuration.
/// </summary>
public sealed record ConnectionDefinition
{
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
}
