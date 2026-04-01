using Confluent.Kafka;
using Minnaloushe.Core.ClientProviders.Kafka.Options;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.Toolbox.DictionaryExtensions;

namespace Minnaloushe.Core.MessageQueues.DependencyInjection.Kafka.Extensions;

public static class OptionsExtensions
{
    public static KafkaClientOptions ToClientOptions(this MessageQueueOptions messageQueueOptions)
    {
        return new KafkaClientOptions()
        {
            ConnectionString = messageQueueOptions.ConnectionString,
            Host = messageQueueOptions.Host,
            Port = messageQueueOptions.Port,
            ServiceName = messageQueueOptions.ServiceName,
            ServiceKey = messageQueueOptions.ServiceKey,
            Username = messageQueueOptions.Username,
            Password = messageQueueOptions.Password,
            Parameters = new KafkaParameters()
            {
                //TODO Refactor defaults
                AutoOffsetReset = messageQueueOptions.Parameters.GetValue<AutoOffsetReset?>(nameof(KafkaParameters.AutoOffsetReset)) ?? AutoOffsetReset.Earliest,
                MaxPollIntervalMs = messageQueueOptions.Parameters.GetValue<int?>(nameof(KafkaParameters.MaxPollIntervalMs)) ?? 300_000,
                EnableAutoCommit = messageQueueOptions.Parameters.GetValue<bool?>(nameof(KafkaParameters.EnableAutoCommit)) ?? false,
                SessionTimeoutMs = messageQueueOptions.Parameters.GetValue<int?>(nameof(KafkaParameters.SessionTimeoutMs)) ?? 45_000,
                TopicConfiguration = messageQueueOptions.Parameters.GetValue<TopicConfiguration>(nameof(KafkaParameters.TopicConfiguration))
                    ?? new TopicConfiguration(),
                DltTopicConfiguration = messageQueueOptions.Parameters.GetValue<TopicConfiguration>(nameof(KafkaParameters.DltTopicConfiguration))
                    ?? new TopicConfiguration(),
            }
        };
    }
}