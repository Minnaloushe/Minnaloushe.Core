using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.ClientProviders.Kafka;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Kafka.Extensions;
using Minnaloushe.Core.Toolbox.StringExtensions;

namespace Minnaloushe.Core.MessageQueues.DependencyInjection.Kafka.Factories;

internal class KafkaConsumerSimpleClientProviderFactory(IServiceProvider serviceProvider) : IKafkaConsumerClientProviderFactory
{
    public bool CanCreate(MessageQueueOptions options)
    {
        return ((options.Username.IsNotNullOrWhiteSpace()
               && options.Password.IsNotNullOrWhiteSpace())
                   || options.ConnectionString.IsNotNullOrWhiteSpace()
               )
               && options.Type.Equals("Kafka", StringComparison.OrdinalIgnoreCase);
    }

    public IKafkaConsumerClientProvider Create(string connectionName)
    {
        var options = serviceProvider.GetRequiredService<IOptionsMonitor<MessageQueueOptions>>();
        var config = options.Get(connectionName);
        return ActivatorUtilities.CreateInstance<KafkaConsumerSimpleClientProvider>(
            serviceProvider,
            connectionName,
            config.ToClientOptions());
    }
}
