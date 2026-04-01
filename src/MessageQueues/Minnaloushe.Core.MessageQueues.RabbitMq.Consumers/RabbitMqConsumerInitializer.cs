using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.ClientProviders.Abstractions;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.Abstractions.Routines;
using RabbitMQ.Client;

namespace Minnaloushe.Core.MessageQueues.RabbitMq.Consumers;

public class RabbitMqConsumerInitializer<TMessage>(
    string name,
    IClientProvider<IConnection> clientProvider,
    IOptionsMonitor<MessageQueueOptions> options,
    IMessageQueueNamingConventionsProvider namingConventionsProvider,
    ILogger<RabbitMqConsumerInitializer<TMessage>> logger
) : IConsumerInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var exchangeName = namingConventionsProvider.GetTopicName<TMessage>();
        var queueName = namingConventionsProvider.GetServiceKey<TMessage>(options.Get(name));

        using var client = clientProvider.Acquire();

        if (!client.IsInitialized)
        {
            throw new InvalidOperationException(
                $"Client provider was not initialized for consumer {name}");
        }

        await using var channel = await client.Client.CreateChannelAsync(cancellationToken: cancellationToken);

        // Create exchange
        try
        {
            await channel.ExchangeDeclareAsync(exchangeName, "fanout", true, false,
                cancellationToken: cancellationToken);
            logger.LogInformation("Exchange '{ExchangeName}' created", exchangeName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create exchange '{ExchangeName}'", exchangeName);
        }

        // Create main queue
        try
        {
            await channel.QueueDeclareAsync(queueName, true, false, false, cancellationToken: cancellationToken);
            logger.LogInformation("Queue '{QueueName}' created", queueName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create queue '{QueueName}'", queueName);
        }

        // Bind main queue to exchange
        try
        {
            await channel.QueueBindAsync(queueName, exchangeName, string.Empty, null, cancellationToken: cancellationToken);
            logger.LogInformation("Queue '{QueueName}' bound to exchange '{ExchangeName}'", queueName, exchangeName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to bind queue '{QueueName}' to exchange '{ExchangeName}'", queueName, exchangeName);
        }
    }
}