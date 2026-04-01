using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.Toolbox.AsyncInitializer;
using Minnaloushe.Core.Toolbox.AsyncInitializer.Extensions;
using Minnaloushe.Core.VaultOptions.ResolvedOptions;
using Minnaloushe.Core.VaultOptions.Vault;

namespace Minnaloushe.Core.ClientProviders.Abstractions.VaultExtensions;

/// <summary>
/// Generic helpers for registering keyed client providers with vault-stored options.
/// Reduces code duplication across similar provider registration patterns.
/// </summary>
public static class KeyedClientProviderRegistrationHelper
{
    /// <summary>
    /// Registers keyed client providers with vault-stored options.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    /// <param name="sectionName">The configuration section name (e.g., "Telegram", "S3Storage")</param>
    /// <param name="providerFactory">Factory function to create the provider instance</param>
    /// <param name="optionsKeySelector">Optional function to customize how the Key property is set on options</param>
    /// <returns>The service collection for chaining</returns>
    public static KeyedSingletonBuilder RegisterKeyedClientProvider<TOptions, TProvider, TProviderImpl, TFactory, TFactoryImpl>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName,
        Func<IServiceProvider, object, TFactory, IResolvedOptions<TOptions>, TProviderImpl> providerFactory,
        //TODO Reconsider key selector approach
        Action<TOptions, string>? optionsKeySelector = null)
        where TOptions : VaultStoredOptions
        where TProvider : class
        where TProviderImpl : class, TProvider, IAsyncInitializer
        where TFactory : class
        where TFactoryImpl : class, TFactory
    {
        var section = configuration.GetSection(sectionName);
        if (!section.Exists())
        {
            return new KeyedSingletonBuilder(typeof(TProvider), services, []);
        }

        var keys = section.GetChildren()
            .Select(c => c.Key)
            .ToList();

        if (keys.Count == 0)
        {
            return new KeyedSingletonBuilder(typeof(TProvider), services, []);
        }

        // Register interface forwarding to the concrete singleton
        services.AddSingleton<IResolvedKeyedOptions<TOptions>>(sp =>
            sp.GetRequiredService<ResolvedKeyedOptions<TOptions>>());

        services.AddSingleton<ResolvedKeyedOptions<TOptions>>();

        // Register the factory
        services.AddSingleton<TFactory, TFactoryImpl>();

        // Default key selector if not provided
        optionsKeySelector ??= (opts, key) =>
        {
            var property = typeof(TOptions).GetProperty("Key");
            property?.SetValue(opts, key);
        };

        foreach (var key in keys)
        {
            services.AddOptions<TOptions>(key)
                .BindConfiguration($"{sectionName}:{key}")
                .Configure(opt => optionsKeySelector(opt, key));

            services.AddKeyedSingleton<TProviderImpl>(key, (sp, serviceKey) =>
            {
                var keyedOptions = sp.GetRequiredService<IResolvedKeyedOptions<TOptions>>();
                var resolvedOptions = keyedOptions.Get((string)serviceKey)
                    ?? throw new InvalidOperationException($"{typeof(TOptions).Name} for key '{key}' have not been initialized. " +
                                                           "Ensure async initializers have completed before accessing the provider.");

                var factory = sp.GetRequiredService<TFactory>();
                return providerFactory(sp, serviceKey, factory, resolvedOptions);
            });

            services.AddKeyedSingleton<TProvider>(key, (sp, serviceKey) =>
                sp.GetRequiredKeyedService<TProviderImpl>(serviceKey));
        }

        services.AddAsyncInitializer<DefaultClientProvidersInitializer<TProviderImpl, TOptions>>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<DefaultClientProvidersInitializer<TProviderImpl, TOptions>>>();

            var initializer = new DefaultClientProvidersInitializer<TProviderImpl, TOptions>(
                sp,
                sp.GetRequiredService<IOptionsMonitor<TOptions>>(),
                sp.GetRequiredService<IVaultOptionsLoader<TOptions>>(),
                sp.GetRequiredService<ResolvedKeyedOptions<TOptions>>(),
                logger);

            foreach (var key in keys)
            {
                initializer.RegisterKey(key);
            }

            return initializer;
        });

        return new KeyedSingletonBuilder(typeof(TProvider), services, [.. keys]);
    }

    /// <summary>
    /// Provides chaining registration of a keyed singleton dependency for the case when dependent service depends on keyed service
    /// </summary>
    /// <remarks>This method facilitates the registration of multiple keyed singleton services, each
    /// associated with a specific key and dependency. The optional iterator parameter can be used to customize service
    /// registration for each key, such as adding additional services or configuration steps.</remarks>
    /// <typeparam name="TDependency">The type of the dependency to be registered as a singleton service.</typeparam>
    /// <typeparam name="TDependencyImpl">The concrete implementation type of the dependency to instantiate for each key.</typeparam>
    /// <param name="iterator">An optional action to perform additional configuration on the service collection for each key before the
    /// singleton service is registered. May be null.</param>
    /// <returns>The original keyed registration instance, enabling method chaining.</returns>
    /// <example>
    /// class ExampleService : IExampleService { ... }
    /// class DependantService(IExampleService svc) : IExampleService { ... }
    /// 
    /// services.AddKeyedClientProvider<IExampleService, ExampleService,...>(...)
    ///   .WithDependency<IDependantService, DependantService>();
    /// Usage:
    /// class ConsumerService(
    ///   [FromKeyedService("registeredKey"] IExampleService, // Passes service registered with "registeredKey"
    ///   [FromKeyedService("registeredKey"] IDependantService) { ... } // Passes properly initializes service that uses keyed IExampleService
    /// </example>
    public static KeyedSingletonBuilder WithDependency<TDependency, TDependencyImpl>(this KeyedSingletonBuilder builder, Action<IServiceCollection, string>? iterator = null)
        where TDependency : class
        where TDependencyImpl : class, TDependency
    {
        foreach (var key in builder.Keys)
        {
            iterator?.Invoke(builder.Services, key);
            builder.Services.AddKeyedSingleton<TDependencyImpl>(key, (sp, k) =>
            {
                var dependency = sp.GetRequiredKeyedService(builder.RootType, k);
                return ActivatorUtilities.CreateInstance<TDependencyImpl>(sp, dependency);
            });
            builder.Services.AddKeyedSingleton<TDependency>(key,
                (sp, k) => sp.GetRequiredKeyedService<TDependencyImpl>(k));
        }

        return builder;
    }

    public static KeyedSingletonBuilder WithDependency(this KeyedSingletonBuilder builder, Action<IServiceCollection, object?> iterator)
    {
        foreach (var key in builder.Keys)
        {
            iterator?.Invoke(builder.Services, key);
        }
        return builder;
    }
}