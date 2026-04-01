using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.ClientProviders.Kafka;
using Minnaloushe.Core.ClientProviders.Kafka.Options;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.Abstractions.Routines;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Kafka.Extensions;
using System.Globalization;

namespace Minnaloushe.Core.MessageQueues.Kafka.Consumers;

internal class KafkaConsumerInitializer<TMessage>(
    string name,
    IOptionsMonitor<MessageQueueOptions> optionsMonitor,
    IKafkaAdminClientProvider adminProvider,
    IMessageQueueNamingConventionsProvider namingConventionsProvider,
    ILogger<KafkaConsumerInitializer<TMessage>> logger
    )
: IConsumerInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        logger.LogConsumerInitializerStarting(name, typeof(TMessage).FullName ?? typeof(TMessage).Name);

        using var lease = adminProvider.Acquire();

        if (lease.Client is null)
        {
            throw new InvalidOperationException(
                $"Kafka admin client for consumer '{name}' is not initialized. " +
                $"Ensure that IAsyncInitializer services are invoked (e.g. host.InvokeAsyncInitializers()) " +
                $"before the hosted services start.");
        }

        var topicName = namingConventionsProvider.GetTopicName<TMessage>();
        logger.LogConsumerTopicResolved(name, topicName);

        var options = optionsMonitor.Get(name);
        logger.LogConsumerOptionsResolved(name, options.ConnectionName, options.Type);

        var client = lease.Client.Client;

        if (client is null)
        {
            throw new InvalidOperationException(
                $"Kafka IAdminClient for consumer '{name}' (topic: '{topicName}') is null. " +
                $"The admin client wrapper was acquired but its inner IAdminClient was not created.");
        }

        // Create main topic only - DLT topic will be created on-demand by the error handling strategy
        await EnsureTopicExistsAsync(client, topicName, options);
    }

    private async Task EnsureTopicExistsAsync(IAdminClient client, string topicName, MessageQueueOptions options)
    {
        try
        {
            var kafkaParameters = options.ToClientOptions().Parameters;
            var desiredConfig = kafkaParameters.TopicConfiguration;
            var metadata = client.GetMetadata(topicName, TimeSpan.FromSeconds(10));

            if (metadata.Topics.Count == 0 || metadata.Topics[0].Error.Code != ErrorCode.NoError)
            {
                await client.CreateTopicsAsync([desiredConfig.ToTopicSpecification(topicName)]);

                logger.LogTopicCreated(topicName);
            }
            else
            {
                await ReconcileTopicAsync(client, topicName, metadata.Topics[0], desiredConfig);
            }
        }
        catch (CreateTopicsException ex) when (ex.Results.Any(r => r.Error.Code == ErrorCode.TopicAlreadyExists))
        {
            // Topic already exists, that's fine
            logger.LogTopicAlreadyExists(topicName);
        }
        catch (Exception ex)
        {
            logger.LogTopicCreationFailed(ex, topicName);
        }
    }

    private async Task ReconcileTopicAsync(
        IAdminClient client,
        string topicName,
        TopicMetadata topicMetadata,
        TopicConfiguration desiredConfig)
    {
        await ReconcilePartitionsAsync(client, topicName, topicMetadata.Partitions.Count, desiredConfig.NumPartitions);
        await ReconcileTopicConfigAsync(client, topicName, desiredConfig);
    }

    private async Task ReconcilePartitionsAsync(
        IAdminClient client,
        string topicName,
        int currentPartitions,
        int desiredPartitions)
    {
        if (desiredPartitions > currentPartitions)
        {
            await client.CreatePartitionsAsync(
            [
                new PartitionsSpecification
                {
                    Topic = topicName,
                    IncreaseTo = desiredPartitions
                }
            ]);

            logger.LogTopicPartitionsExpanded(topicName, currentPartitions, desiredPartitions);
        }
        else if (desiredPartitions < currentPartitions)
        {
            logger.LogTopicPartitionReductionSkipped(topicName, currentPartitions, desiredPartitions);
        }
    }

    private async Task ReconcileTopicConfigAsync(
        IAdminClient client,
        string topicName,
        TopicConfiguration desiredConfig)
    {
        var configResource = new ConfigResource
        {
            Type = ResourceType.Topic,
            Name = topicName
        };

        var configEntries = new List<ConfigEntry>
        {
            new() { Name = "retention.ms", Value = desiredConfig.RetentionTime.TotalMilliseconds.ToString(CultureInfo.InvariantCulture) },
            new() { Name = "retention.bytes", Value = desiredConfig.RetentionBytes.ToString() },
            new() { Name = "cleanup.policy", Value = desiredConfig.CleanUpPolicy.ToConfig() },
            new() { Name = "delete.retention.ms", Value = desiredConfig.DeleteRetentionTime.TotalMilliseconds.ToString(CultureInfo.InvariantCulture) }
        };

        await client.AlterConfigsAsync(new Dictionary<ConfigResource, List<ConfigEntry>>
        {
            { configResource, configEntries }
        });

        logger.LogTopicConfigurationUpdated(topicName);
    }
}