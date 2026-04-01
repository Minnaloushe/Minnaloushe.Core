using Microsoft.Extensions.DependencyInjection;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Factories;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Models;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Registries;
using Minnaloushe.Core.MessageQueues.Routines;

namespace Minnaloushe.Core.MessageQueues.DependencyInjection.Extensions;

public record ProducerOptions<TMessage>
{
    public Func<TMessage, string>? KeySelector { get; init; }
    public bool ResolveMessageTypeAtRuntime { get; init; } = false;
}
/// <summary>
/// Extension methods for producer registration.
/// </summary>
public static class ProducerRegistrationExtensions
{
    /// <summary>
    /// Registers a producer for the specified message type.
    /// The producer name must match a configuration entry at MessageQueues:Connections:{connectionName}.
    /// The producer can be resolved using either the producer name or the connection name as a service key.
    /// </summary>
    /// <typeparam name="TMessage">The type of message the producer handles.</typeparam>
    /// <param name="builder">The message queue builder.</param>
    /// <param name="connectionName">The connection name matching configuration.</param>
    /// <param name="name">The producer name. If null, uses MqNaming.GetSafeName&lt;TMessage&gt;().</param>
    /// <param name="producerOptions">Optional producer options including key selector and runtime resolution settings.</param>
    /// <returns>The builder for chaining.</returns>
    /// <remarks>Registration with name parameter is implemented for the cases when different options or instances are required.</remarks>
    public static MessageQueueBuilder AddProducer<TMessage>(this MessageQueueBuilder builder, string connectionName, string? name = null, ProducerOptions<TMessage>? producerOptions = null)
    {
        var producerName = name ?? MqNaming.GetSafeName<TMessage>();

        builder.ProducerRegistrations.Add(new ProducerRegistration
        {
            MessageType = typeof(TMessage),
            Name = producerName,
            ConnectionName = connectionName,
            ProducerOptions = producerOptions
        });

        return builder;
    }

    /// <summary>
    /// Registers a producer factory for specified connection types.
    /// </summary>
    /// <param name="connectionTypes">The connection type names to register the factory for.</param>
    /// <param name="factory">The producer factory to register.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection RegisterProducerFactory(this IServiceCollection services, IEnumerable<string> connectionTypes, IProducerFactory factory)
    {
        var registry = services.GetOrCreateProducerFactoryRegistry();

        foreach (var connectionType in connectionTypes)
        {
            registry.RegisterFactory(connectionType, factory);
        }

        return services;
    }

    /// <summary>
    /// Gets or creates the producer factory registry.
    /// </summary>
    internal static IProducerFactoryRegistry GetOrCreateProducerFactoryRegistry(this IServiceCollection services)
    {
        var registryDescriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(IProducerFactoryRegistry));

        if (registryDescriptor == null)
        {
            var registry = new ProducerFactoryRegistry();
            services.AddSingleton<IProducerFactoryRegistry>(registry);
            return registry;
        }

        return registryDescriptor.ImplementationInstance is IProducerFactoryRegistry existingRegistry
            ? existingRegistry
            : throw new InvalidOperationException(
            "IProducerFactoryRegistry is registered but not as a singleton instance. " +
            "Ensure it's registered using AddSingleton with an instance.");
    }

    /// <summary>
    /// Registers producer services for all programmatic producer registrations.
    /// Uses the registered producer factory for each connection type.
    /// </summary>
    internal static void RegisterProducers(this MessageQueueRegistrationContext context, string connectionType)
    {
        var factoryRegistryDescriptor = context.Services.FirstOrDefault(sd => sd.ServiceType == typeof(IProducerFactoryRegistry));

        if (factoryRegistryDescriptor?.ImplementationInstance is not IProducerFactoryRegistry factoryRegistry)
        {
            return;
        }

        var factory = factoryRegistry.GetFactory(connectionType);

        if (factory is null)
        {
            return;
        }

        foreach (var registration in context.ProducerRegistrations)
        {
            var registrar = factory.CreateRegistrar(registration.MessageType);
            registrar.Register(context.Services, registration.Name!, context.ConnectionName, registration.ProducerOptions);
        }
    }
}
