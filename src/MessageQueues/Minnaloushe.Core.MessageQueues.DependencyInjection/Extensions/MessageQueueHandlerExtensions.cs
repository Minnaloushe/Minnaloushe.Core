using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.ClientProviders.Abstractions.Factories;
using Minnaloushe.Core.ClientProviders.Abstractions.FactorySelector;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.Abstractions.Routines;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Factories;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Models;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Registries;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Routines;
using Minnaloushe.Core.Toolbox.AsyncInitializer;
using Minnaloushe.Core.Toolbox.JsonConfiguration;

namespace Minnaloushe.Core.MessageQueues.DependencyInjection.Extensions;

/// <summary>
/// Extension methods for registering message queue client provider handlers.
/// Provides common logic for RabbitMQ, Kafka, and other message queue providers.
/// </summary>
public static class MessageQueueHandlerExtensions
{
    /// <param name="services">The service collection to which the message queue services will be added. Cannot be null.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds message queue services and configuration to the specified service collection.
        /// </summary>
        /// <remarks>This method registers the required services for message queue functionality, including
        /// configuration and handler registration. Call this method during application startup to enable message queue
        /// support.</remarks>
        /// <param name="configuration">The application configuration used to configure message queue services. Cannot be null.</param>
        /// <returns>A builder that can be used to further configure message queue services.</returns>
        public MessageQueueBuilder AddMessageQueues(IConfiguration configuration)
        {
            services.AddJsonConfiguration();

            var handlerRegistry = services.GetOrCreateMessageQueueHandlerRegistry();
            var connectionTypeRegistry = services.GetOrCreateConnectionTypeRegistry();

            services.AddSingleton<IMessageQueueNamingConventionsProvider, MessageQueueNamingConventionsProvider>();

            return new MessageQueueBuilder(services, handlerRegistry, connectionTypeRegistry, configuration);
        }

        /// <summary>
        /// Registers a message queue handler for the specified connection types in the dependency injection container.
        /// </summary>
        /// <remarks>This method enables multiple message queue connection types to be associated with a single
        /// handler registration. Each connection type in <paramref name="connectionTypes"/> will be registered with the
        /// provided <paramref name="handler"/>.</remarks>
        /// <param name="connectionTypes">A collection of connection type names for which the handler will be registered. Cannot be null or contain null
        /// elements.</param>
        /// <param name="handler">An action that configures the message queue handler for each specified connection type. Cannot be null.</param>
        /// <returns>The same <see cref="IServiceCollection"/> instance so that additional calls can be chained.</returns>
        public IServiceCollection RegisterMessageQueueHandler(IEnumerable<string> connectionTypes,
            Action<MessageQueueRegistrationContext> handler)
        {
            var registry = services.GetOrCreateMessageQueueHandlerRegistry();

            foreach (var connectionType in connectionTypes)
            {
                registry.RegisterHandler(connectionType, handler);
            }

            return services;
        }

        /// <summary>
        /// Registers a message queue handler for the specified connection type in the dependency injection container.
        /// </summary>
        /// <param name="connectionType">The type of message queue connection for which the handler is being registered. Cannot be null or empty.</param>
        /// <param name="handler">A delegate that configures the message queue handler using the provided registration context. Cannot be null.</param>
        /// <returns>The same instance of <see cref="IServiceCollection"/> that was provided, to allow for method chaining.</returns>
        public IServiceCollection RegisterMessageQueueHandler(string connectionType,
            Action<MessageQueueRegistrationContext> handler)
        {
            return services.RegisterMessageQueueHandler([connectionType], handler);
        }

        /// <summary>
        /// Gets or creates the message queue handler registry.
        /// </summary>
        internal IMessageQueueHandlerRegistry GetOrCreateMessageQueueHandlerRegistry()
        {
            var registryDescriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(IMessageQueueHandlerRegistry));

            if (registryDescriptor == null)
            {
                var registry = new MessageQueueHandlerRegistry();
                services.AddSingleton<IMessageQueueHandlerRegistry>(registry);
                return registry;
            }

            return registryDescriptor.ImplementationInstance as IMessageQueueHandlerRegistry ?? throw new InvalidOperationException(
                "IMessageQueueHandlerRegistry is registered but not as a singleton instance. " +
                "Ensure it's registered using AddSingleton with an instance.");
        }

        /// <summary>
        /// Gets or creates the connection type registry.
        /// </summary>
        internal IConnectionTypeRegistry GetOrCreateConnectionTypeRegistry()
        {
            var registryDescriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(IConnectionTypeRegistry));

            if (registryDescriptor == null)
            {
                var registry = new ConnectionTypeRegistry();
                services.AddSingleton<IConnectionTypeRegistry>(registry);
                return registry;
            }

            return registryDescriptor.ImplementationInstance as IConnectionTypeRegistry ?? throw new InvalidOperationException(
                "IConnectionTypeRegistry is registered but not as a singleton instance. " +
                "Ensure it's registered using AddSingleton with an instance.");
        }

        /// <summary>
        /// Gets or creates the consumer factory registry.
        /// </summary>
        internal IConsumerFactoryRegistry GetOrCreateConsumerFactoryRegistry()
        {
            var registryDescriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(IConsumerFactoryRegistry));

            if (registryDescriptor == null)
            {
                var registry = new ConsumerFactoryRegistry();
                services.AddSingleton<IConsumerFactoryRegistry>(registry);
                return registry;
            }

            return registryDescriptor.ImplementationInstance as IConsumerFactoryRegistry ?? throw new InvalidOperationException(
                "IConsumerFactoryRegistry is registered but not as a singleton instance. " +
                "Ensure it's registered using AddSingleton with an instance.");
        }

        /// <summary>
        /// Registers a consumer factory for specified connection types.
        /// </summary>
        /// <param name="connectionTypes">The connection type names to register the factory for.</param>
        /// <param name="factory">The consumer factory to register.</param>
        /// <returns>The service collection for chaining.</returns>
        public IServiceCollection RegisterConsumerFactory(IEnumerable<string> connectionTypes, IConsumerFactory factory)
        {
            var registry = services.GetOrCreateConsumerFactoryRegistry();

            foreach (var connectionType in connectionTypes)
            {
                registry.RegisterFactory(connectionType, factory);
            }

            return services;
        }
    }


    extension(MessageQueueRegistrationContext context)
    {
        /// <summary>
        /// Registers a keyed client provider using factory selection pattern.
        /// This is the common registration logic shared by RabbitMQ, Kafka, and other providers.
        /// </summary>
        /// <typeparam name="TProvider">The provider interface type.</typeparam>
        /// <typeparam name="TFactory">The factory interface type.</typeparam>
        /// <typeparam name="TSelector">The factory selector interface type.</typeparam>
        public void RegisterKeyedProvider<TProvider, TFactory, TSelector>()
            where TProvider : class
            where TFactory : IClientProviderFactory<TProvider, MessageQueueOptions>
            where TSelector : class, IClientProviderFactorySelector<TProvider, TFactory, MessageQueueOptions>
        {
            // Register the provider with deferred factory selection
            context.Services.AddKeyedSingleton<TProvider>(context.ConnectionName, (sp, key) =>
            {
                var options = sp.GetRequiredService<IOptionsMonitor<MessageQueueOptions>>().Get(context.ConnectionName);
                var factories = sp.GetServices<TFactory>();
                var selector = sp.GetRequiredService<TSelector>();

                var factory = selector.SelectFactory(options, factories);

                return factory == null
                    ? throw new InvalidOperationException(
                        $"No registered {typeof(TFactory).Name} can handle connection '{context.ConnectionName}'. " +
                        $"ServiceName='{options.ServiceName}', Host='{(string.IsNullOrWhiteSpace(options.Host) ? "(empty)" : "(set)")}'")
                    : factory.Create(context.ConnectionName);
            });

            // Register as IAsyncInitializer for initialization
            context.Services.AddSingleton<IAsyncInitializer>(sp =>
            {
                var provider = sp.GetRequiredKeyedService<TProvider>(context.ConnectionName);
                return provider is not IAsyncInitializer initializer
                    ? throw new InvalidOperationException(
                        $"Provider for connection '{context.ConnectionName}' does not implement IAsyncInitializer.")
                    : initializer;
            });

            // Register keyed providers for all consumers using this connection
            foreach (var consumer in context.Consumers)
            {
                context.Services.AddKeyedSingleton<TProvider>(consumer.Name,
                    (sp, key) => sp.GetRequiredKeyedService<TProvider>(context.ConnectionName));
            }
        }

        /// <summary>
        /// Registers consumer hosted services for all programmatic consumer registrations in this context.
        /// Uses the registered consumer factory for the connection type.
        /// Also registers keyed client provider aliases for each programmatically registered consumer.
        /// </summary>
        /// <param name="connectionType">The connection type (e.g., "rabbitmq", "kafka").</param>
        public void RegisterConsumerHostedServices(string connectionType)
        {
            var factoryRegistryDescriptor = context.Services.FirstOrDefault(sd => sd.ServiceType == typeof(IConsumerFactoryRegistry));

            if (factoryRegistryDescriptor?.ImplementationInstance is not IConsumerFactoryRegistry factoryRegistry)
            {
                return; // No consumer factories registered
            }

            var factory = factoryRegistry.GetFactory(connectionType);

            if (factory is null)
            {
                return; // No factory for this connection type
            }

            foreach (var registration in context.ConsumerRegistrations)
            {
                var consumerDef = context.Consumers.FirstOrDefault(
                    c => c.Name.Equals(registration.Name, StringComparison.OrdinalIgnoreCase));

                if (consumerDef is null)
                {
                    continue;
                }

                var registrar = factory.CreateRegistrar(registration.MessageType);
                registrar.Register(context.Services, registration.Name!, context.ConnectionName, registration.Parallelism);
            }
        }

        /// <summary>
        /// Registers keyed client provider aliases for programmatically registered consumers.
        /// This allows ConsumerHostedService to resolve providers by consumer name.
        /// </summary>
        /// <typeparam name="TProvider">The client provider type (e.g., IClientProvider&lt;IChannel&gt;).</typeparam>
        public void RegisterKeyedProviderAliasesForProgrammaticConsumers<TProvider>()
            where TProvider : class
        {
            foreach (var registration in context.ConsumerRegistrations)
            {
                var consumerDef = context.Consumers.FirstOrDefault(
                    c => c.Name.Equals(registration.Name, StringComparison.OrdinalIgnoreCase));

                if (consumerDef is null)
                {
                    continue;
                }

                // Register an alias from consumer name -> connection provider
                context.Services.AddKeyedSingleton<TProvider>(registration.Name!,
                    (sp, key) => sp.GetRequiredKeyedService<TProvider>(context.ConnectionName));
            }
        }
    }

    extension(MessageQueueBuilder builder)
    {
        /// <summary>
        /// Registers a consumer factory for specified connection types via the builder.
        /// </summary>
        /// <param name="connectionTypes">The connection type names to register the factory for.</param>
        /// <param name="factory">The consumer factory to register.</param>
        /// <returns>The builder for chaining.</returns>
        public MessageQueueBuilder WithConsumerFactory(IEnumerable<string> connectionTypes, IConsumerFactory factory)
        {
            builder.Services.RegisterConsumerFactory(connectionTypes, factory);
            return builder;
        }

        /// <summary>
        /// Registers a consumer factory for a single connection type via the builder.
        /// </summary>
        /// <param name="connectionType">The connection type name to register the factory for.</param>
        /// <param name="factory">The consumer factory to register.</param>
        /// <returns>The builder for chaining.</returns>
        public MessageQueueBuilder WithConsumerFactory(string connectionType, IConsumerFactory factory)
        {
            return builder.WithConsumerFactory([connectionType], factory);
        }
    }
}