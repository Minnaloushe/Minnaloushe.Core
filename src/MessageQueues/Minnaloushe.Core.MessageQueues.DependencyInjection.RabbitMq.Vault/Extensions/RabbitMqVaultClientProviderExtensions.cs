using Microsoft.Extensions.DependencyInjection;
using Minnaloushe.Core.ClientProviders.Abstractions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Extensions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Models;
using Minnaloushe.Core.MessageQueues.DependencyInjection.RabbitMq.Factories;
using Minnaloushe.Core.MessageQueues.DependencyInjection.RabbitMq.FactorySelectors;
using Minnaloushe.Core.MessageQueues.DependencyInjection.RabbitMq.Vault.Factories;
using RabbitMQ.Client;

namespace Minnaloushe.Core.MessageQueues.DependencyInjection.RabbitMq.Vault.Extensions;

/// <summary>
/// Extension methods for registering Vault-based RabbitMQ client providers.
/// </summary>
public static class RabbitMqVaultClientProviderExtensions
{
    /// <summary>
    /// Registers Vault-based RabbitMQ client providers for connections using the "rabbit-static" type.
    /// This enables RabbitMQ connections that retrieve credentials from Vault.
    /// Registers "rabbit-static" and "rabbitmq-static" connection types in the "rabbit" provider group.
    /// </summary>
    /// <param name="builder">The message queue builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static MessageQueueBuilder AddVaultRabbitMqClientProviders(this MessageQueueBuilder builder)
    {
        // Register connection types in the "rabbit" provider group
        builder.RegisterConnectionTypes(["rabbit-static", "rabbitmq-static"], "rabbit");

        // Register the Vault-specific factory
        builder.Services.AddSingleton<IRabbitMqClientProviderFactory, RabbitMqStaticClientProviderFactory>();
        builder.Services.AddSingleton<IRabbitMqClientProviderFactorySelector, RabbitMqClientProviderFactorySelector>();

        // Register handler for both rabbitmq and rabbit-static types
        builder.Services.RegisterMessageQueueHandler(["rabbit-static", "rabbitmq-static"], context =>
        {
            context.RegisterKeyedProvider<
                IClientProvider<IConnection>,
                IRabbitMqClientProviderFactory,
                IRabbitMqClientProviderFactorySelector>();
        });

        return builder;
    }
}
