using Microsoft.Extensions.DependencyInjection;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Models;
using Minnaloushe.Core.MessageQueues.Routines;

namespace Minnaloushe.Core.MessageQueues.DependencyInjection.Extensions;

public static class ConsumerRegistrationExtensions
{
    /// <summary>
    /// Registers a consumer for the specified message type.
    /// The consumer name must match a configuration entry at MessageQueues:Consumers:{name}.
    /// </summary>
    /// <typeparam name="TMessage">The type of message the consumer handles.</typeparam>
    /// <typeparam name="TConsumer">The type of message handler, must implement <see cref="IConsumer{TMessage}"/></typeparam>
    /// <param name="builder">The message queue builder.</param>
    /// <param name="name">The consumer name matching configuration. If null, uses MqNaming.GetSafeName&lt;TMessage&gt;().</param>
    /// <returns>The builder for chaining.</returns>
    public static MessageQueueBuilder AddConsumer<TMessage, TConsumer>(this MessageQueueBuilder builder, string? consumerName = null)
    where TConsumer : class, IConsumer<TMessage>
    {
        var name = consumerName ?? MqNaming.GetSafeName<TMessage>();

        builder.ConsumerRegistrations.Add(new ConsumerRegistration
        {
            MessageType = typeof(TMessage),
            Name = name
        });

        builder.Services.AddScoped<IConsumer<TMessage>, TConsumer>();

        return builder;
    }
}
