using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Minnaloushe.Core.MessageQueues.Abstractions;

/// <summary>
/// Extension methods for registering consumer hosted services.
/// </summary>
public static class ConsumerHostedServiceExtensions
{
    /// <summary>
    /// Registers a ConsumerHostedService for the specified message and client types.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <typeparam name="TClient">The client type used by the provider.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="consumerName">The name of the consumer.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddConsumerHostedService<TMessage, TClient>(
        this IServiceCollection services,
        string consumerName)
        where TClient : class
    {
        services.AddHostedService(sp =>
        {
            var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<MessageQueueOptions>>();
            var logger = sp.GetRequiredService<ILogger<ConsumerHostedService<TMessage, TClient>>>();

            return new ConsumerHostedService<TMessage, TClient>(
                consumerName,
                sp,
                optionsMonitor,
                logger);
        });

        return services;
    }
}
