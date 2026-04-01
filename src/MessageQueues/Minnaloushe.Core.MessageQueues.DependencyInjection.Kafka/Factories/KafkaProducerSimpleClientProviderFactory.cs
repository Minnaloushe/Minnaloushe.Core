using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.ClientProviders.Kafka;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Kafka.Extensions;
using Minnaloushe.Core.Toolbox.StringExtensions;

namespace Minnaloushe.Core.MessageQueues.DependencyInjection.Kafka.Factories;

public class KafkaProducerSimpleClientProviderFactory(IServiceProvider serviceProvider)
    : IKafkaProducerClientProviderFactory
{
    public bool CanCreate(MessageQueueOptions options)
        => (options.Host.IsNotNullOrWhiteSpace() && options.Port != 0)
           || (options.ConnectionString.IsNotNullOrWhiteSpace()
               && options.Type.Equals("kafka", StringComparison.OrdinalIgnoreCase));

    public IKafkaProducerClientProvider Create(string connectionName)
    {
        var optionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<MessageQueueOptions>>();
        var config = optionsMonitor.Get(connectionName);
        return ActivatorUtilities.CreateInstance<KafkaProducerSimpleClientProvider>(
            serviceProvider,
            connectionName,
            config.ToClientOptions());
    }
}
