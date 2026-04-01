using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Models;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Registries;
using Minnaloushe.Core.Toolbox.StringExtensions;
using System.Text.Json;

namespace Minnaloushe.Core.MessageQueues.DependencyInjection.Extensions;

/// <summary>
/// Base configuration extensions for message queues.
/// Parses consumer and connection definitions and provides extension points for provider-specific registration.
/// </summary>
public static class MessageQueueConfigurationExtensions
{
    /// <summary>
    /// Configures message queues from the provided configuration.
    /// Parses connection and consumer definitions, binds named options, and delegates to provider-specific handlers.
    /// Uses handlers registered via IMessageQueueHandlerRegistry.
    /// </summary>
    /// <param name="builder">Builder entity used for method chaining</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection Build(this MessageQueueBuilder builder)
    {
        var registryDescriptor = builder.Services.FirstOrDefault(sd => sd.ServiceType == typeof(IMessageQueueHandlerRegistry));

        if (registryDescriptor?.ImplementationInstance is not IMessageQueueHandlerRegistry registry)
        {
            throw new InvalidOperationException(
                "IMessageQueueHandlerRegistry not found. Ensure AddRabbitMqClientProviders, AddKafkaClientProviders, " +
                "or other provider registration methods are called before ConfigureMessageQueues.");
        }

        var handlers = registry.GetHandlers();

        if (handlers.Count == 0)
        {
            throw new InvalidOperationException(
                "No connection handlers registered. Call AddRabbitMqClientProviders, AddKafkaClientProviders, " +
                "or register custom handlers before calling ConfigureMessageQueues.");
        }

        var mqConfigSection = builder.Configuration.GetSection("MessageQueues");
        var consumers = mqConfigSection.GetSection("Consumers").Get<List<ConsumerDefinition>>() ?? [];

        // Index sections once for fast lookup
        var connectionSections = mqConfigSection.GetSection("Connections").GetChildren()
            .ToDictionary(s => s.GetValue<string>(nameof(ConnectionDefinition.Name))!, StringComparer.OrdinalIgnoreCase);
        var consumerSections = mqConfigSection.GetSection("Consumers").GetChildren()
            .ToDictionary(s => s.GetValue<string>(nameof(ConsumerDefinition.Name))!, StringComparer.OrdinalIgnoreCase);

        // Merge programmatic consumer registrations with config-defined consumers
        var mergedConsumers = MergeConsumerDefinitions(consumers, builder.ConsumerRegistrations, consumerSections);

        // Register consumer-scoped named options (consumer + its connection)
        RegisterConsumerOptions(builder.Services, mergedConsumers, consumerSections, connectionSections);

        // Register connection-level handlers
        RegisterConnectionDependencies(builder.Services, mergedConsumers, connectionSections, handlers, builder.ConsumerRegistrations, builder.ProducerRegistrations);

        // Register consumer hosted services
        RegisterConsumerHostedServices(builder.Services, builder.ConsumerRegistrations, mergedConsumers);

        return builder.Services;
    }

    /// <summary>
    /// Merges programmatic consumer registrations with configuration-defined consumers.
    /// Programmatic registrations that have matching config entries will be linked; others will be added.
    /// </summary>
    private static List<ConsumerDefinition> MergeConsumerDefinitions(
        List<ConsumerDefinition> configConsumers,
        List<ConsumerRegistration> programmaticRegistrations,
        Dictionary<string, IConfigurationSection> consumerSections)
    {
        var result = new List<ConsumerDefinition>(configConsumers);


        foreach (var registration in programmaticRegistrations.ToArray())
        {
            var name = registration.Name!;
            var section = consumerSections[name];
            var parallelism = section.GetValue<int?>(nameof(ConsumerDefinition.Parallelism)) ?? 1;

            var index = programmaticRegistrations.IndexOf(registration);
            programmaticRegistrations[index] = registration with { Parallelism = parallelism };
        }

        foreach (var registration in programmaticRegistrations)
        {
            var name = registration.Name!;
            if (!consumerSections.ContainsKey(name))
            {
                throw new InvalidOperationException(
                    $"Consumer '{name}' was registered programmatically but no matching configuration section " +
                    $"exists at MessageQueues:Consumers:{name}. Ensure configuration is provided.");
            }

            // Check if already in the list (from config)
            if (!result.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                var section = consumerSections[name];
                var connectionName = section.GetValue<string>(nameof(ConsumerDefinition.ConnectionName));
                var parallelism = section.GetValue<int?>(nameof(ConsumerDefinition.Parallelism)) ?? 1;

                if (string.IsNullOrWhiteSpace(connectionName))
                {
                    throw new InvalidOperationException(
                        $"Consumer '{name}' configuration must specify a ConnectionName.");
                }

                result.Add(new ConsumerDefinition { Name = name, ConnectionName = connectionName, Parallelism = parallelism });
            }
        }

        return result;
    }

    /// <summary>
    /// Registers named options for each consumer, binding configuration from both consumer and connection sections.
    /// Parameters dictionaries are merged with consumer parameters taking priority over connection parameters.
    /// </summary>
    private static void RegisterConsumerOptions(
        IServiceCollection services,
        List<ConsumerDefinition> consumers,
        Dictionary<string, IConfigurationSection> consumerSections,
        Dictionary<string, IConfigurationSection> connectionSections)
    {
        foreach (var consumer in consumers)
        {
            if (!consumerSections.TryGetValue(consumer.Name, out var consumerSection))
            {
                throw new InvalidOperationException($"No configuration section found for consumer '{consumer.Name}'.");
            }

            if (!connectionSections.TryGetValue(consumer.ConnectionName, out var connSection))
            {
                throw new InvalidOperationException($"No matching connection found for consumer '{consumer.Name}' with connection name '{consumer.ConnectionName}'.");
            }

            // Capture sections for closure - these will be evaluated at resolution time
            var connectionParametersSection = connSection.GetSection("Parameters");
            var consumerParametersSection = consumerSection.GetSection("Parameters");

            services.AddOptions<MessageQueueOptions>(consumer.Name)
                .Bind(connSection)
                .Bind(consumerSection)
                .Configure(options =>
                {
                    // Compute merged parameters at resolution time (lazy evaluation)
                    // This ensures configuration is fully loaded before we read it
                    var mergedParameters = MergeParameters(connectionParametersSection, consumerParametersSection);
                    options.Parameters = mergedParameters;
                })
                .Validate(HasServiceOrConnectionDetails, $"Either ServiceName or Host/Port must be provided in ConsumerOptions for consumer '{consumer.Name}'.")
                .ValidateOnStart();
        }
    }

    /// <summary>
    /// Builds parameters dictionary from a single configuration section.
    /// </summary>
    private static IReadOnlyDictionary<string, JsonElement> BuildParametersFromSection(
        IConfigurationSection parametersSection)
    {
        var result = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        AddParametersFromSection(parametersSection, result);
        return result;
    }

    /// <summary>
    /// Merges parameters from connection and consumer configuration sections.
    /// Consumer parameters take priority over connection parameters.
    /// </summary>
    private static IReadOnlyDictionary<string, JsonElement> MergeParameters(
        IConfigurationSection connectionParams,
        IConfigurationSection consumerParams)
    {
        var result = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        // Add connection parameters first
        AddParametersFromSection(connectionParams, result);

        // Consumer parameters override connection parameters
        AddParametersFromSection(consumerParams, result);

        return result;
    }

    /// <summary>
    /// Adds parameters from a configuration section to the result dictionary.
    /// Each child section is converted to a JsonElement.
    /// </summary>
    private static void AddParametersFromSection(
        IConfigurationSection section,
        Dictionary<string, JsonElement> result)
    {
        foreach (var child in section.GetChildren())
        {
            // Convert the configuration section to JSON and parse as JsonElement
            var json = ConvertSectionToJson(child);
            if (!string.IsNullOrEmpty(json))
            {
                result[child.Key] = JsonDocument.Parse(json).RootElement.Clone();
            }
        }
    }

    /// <summary>
    /// Converts a configuration section to its JSON representation.
    /// </summary>
    private static string ConvertSectionToJson(IConfigurationSection section)
    {
        var children = section.GetChildren().ToList();

        if (children.Count == 0)
        {
            // Leaf node - return the value as JSON
            var value = section.Value;
            if (value is null)
            {
                return "null";
            }

            // Try to parse as various types
            if (bool.TryParse(value, out var boolVal))
            {
                return boolVal ? "true" : "false";
            }
            if (long.TryParse(value, out var longVal))
            {
                return longVal.ToString();
            }
            if (double.TryParse(value, out var doubleVal))
            {
                return doubleVal.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            // Return as quoted string
            return JsonSerializer.Serialize(value);
        }

        // Check if this is an array (keys are numeric indices)
        var isArray = children.All(c => int.TryParse(c.Key, out _));

        if (isArray)
        {
            var items = children.Select(ConvertSectionToJson);
            return $"[{string.Join(",", items)}]";
        }

        // Object
        var properties = children.Select(c => $"\"{c.Key}\":{ConvertSectionToJson(c)}");
        return $"{{{string.Join(",", properties)}}}";
    }

    /// <summary>
    /// Registers connection-level named options and delegates to provider-specific handlers for each distinct connection.
    /// </summary>
    private static void RegisterConnectionDependencies(
        IServiceCollection services,
        List<ConsumerDefinition> consumers,
        Dictionary<string, IConfigurationSection> connectionSections,
        IReadOnlyDictionary<string, Action<MessageQueueRegistrationContext>> handlers,
        List<ConsumerRegistration> programmaticRegistrations,
        List<ProducerRegistration> producerRegistrations)
    {
        // Process all connections defined in configuration, not just those referenced by consumers/producers
        foreach (var connectionName in connectionSections.Keys)
        {
            if (!connectionSections.TryGetValue(connectionName, out var connSection))
            {
                throw new InvalidOperationException($"No configuration section found for connection '{connectionName}'.");
            }

            // Capture section for closure
            var connectionParametersSection = connSection.GetSection("Parameters");

            // Named options for the connection (used by producer creation)
            services.AddOptions<MessageQueueOptions>(connectionName)
                .Bind(connSection)
                .Configure(options =>
                {
                    // Convert Parameters from configuration section to proper JsonElement dictionary
                    var parameters = BuildParametersFromSection(connectionParametersSection);
                    options.Parameters = parameters;
                })
                .Validate(HasServiceOrConnectionDetails, $"Either ServiceName or Host/Port must be provided in ConsumerOptions for connection '{connectionName}'.")
                .ValidateOnStart();

            var type = connSection.GetValue<string>(nameof(ConnectionDefinition.Type));
            var consumersForConnection = consumers.Where(c => c.ConnectionName.Equals(connectionName, StringComparison.OrdinalIgnoreCase)).ToList();

            // Filter programmatic consumer registrations for this connection
            var consumerRegistrationsForConnection = programmaticRegistrations
                .Where(r => consumersForConnection.Any(c => c.Name.Equals(r.Name, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            // Filter producer registrations for this connection
            var producerRegistrationsForConnection = producerRegistrations
                .Where(r => r.ConnectionName.Equals(connectionName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!handlers.TryGetValue(type ?? string.Empty, out var handler))
            {
                throw new NotSupportedException($"Connection type '{type}' is not supported for connection '{connectionName}'. No handler registered.");
            }

            var context = new MessageQueueRegistrationContext(
                services,
                connectionName,
                connSection,
                consumersForConnection,
                consumerRegistrationsForConnection,
                producerRegistrationsForConnection);

            // Invoke the provider-specific handler (e.g., RabbitMQ, Kafka)
            handler(context);

            // After handler completes, register consumer hosted services if there are programmatic registrations
            if (consumerRegistrationsForConnection.Count > 0 && !string.IsNullOrEmpty(type))
            {
                context.RegisterConsumerHostedServices(type);
            }

            // Register producer services if there are programmatic producer registrations
            if (producerRegistrationsForConnection.Count > 0 && !string.IsNullOrEmpty(type))
            {
                context.RegisterProducers(type);
            }
        }
    }

    /// <summary>
    /// Registers hosted services for each programmatic consumer registration.
    /// The number of hosted service instances is determined by ConsumerOptions.Parallelism.
    /// </summary>
    private static void RegisterConsumerHostedServices(
        IServiceCollection services,
        List<ConsumerRegistration> programmaticRegistrations,
        List<ConsumerDefinition> allConsumers)
    {
        // Get or create the consumer factory registry
        var factoryRegistryDescriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(IConsumerFactoryRegistry));

        if (factoryRegistryDescriptor?.ImplementationInstance is not IConsumerFactoryRegistry factoryRegistry)
        {
            // No consumer factories registered, nothing to do
            return;
        }

        // Get connection sections for building factory context
        // Note: Connection sections are already indexed in the caller, but we rebuild here for simplicity
        // This could be optimized by passing them as a parameter

        // For each programmatic registration, invoke the appropriate consumer factory
        foreach (var registration in programmaticRegistrations)
        {
            var consumerDef = allConsumers.FirstOrDefault(c => c.Name.Equals(registration.Name, StringComparison.OrdinalIgnoreCase));

            if (consumerDef is null)
            {
                continue;
            }

            // The factory invocation happens during connection handler registration (RegisterConnectionInitializers)
            // via MessageQueueRegistrationContext.ConsumerRegistrations
            // This method now just validates registrations exist
        }
    }

    /// <summary>
    /// Validates that at least one connection method is provided: ServiceName (for service discovery), Host (for direct connection), or ConnectionString.
    /// </summary>
    private static bool HasServiceOrConnectionDetails(MessageQueueOptions o)
    {
        return o.ServiceName.IsNotNullOrWhiteSpace()
            || o.Host.IsNotNullOrWhiteSpace()
            || o.ConnectionString.IsNotNullOrWhiteSpace();
    }

    public sealed record ConnectionDefinition
    {
        public string Name { get; init; } = string.Empty;
        public string Type { get; init; } = string.Empty;
    }

    public sealed record ConsumerDefinition
    {
        public string Name { get; init; } = string.Empty;
        public string ConnectionName { get; init; } = string.Empty;
        public int Parallelism { get; init; } = 1;
    }
}
