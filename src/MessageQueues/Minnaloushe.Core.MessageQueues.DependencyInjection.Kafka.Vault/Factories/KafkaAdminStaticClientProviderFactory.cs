using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.ClientProviders.Kafka;
using Minnaloushe.Core.ClientProviders.Kafka.Vault;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Kafka.Extensions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Kafka.Factories;
using Minnaloushe.Core.Toolbox.StringExtensions;

namespace Minnaloushe.Core.MessageQueues.DependencyInjection.Kafka.Vault.Factories;

internal class KafkaAdminStaticClientProviderFactory(IServiceProvider serviceProvider) : IKafkaAdminClientProviderFactory
{
    public bool CanCreate(MessageQueueOptions options)
    {
        return options.Username.IsNullOrWhiteSpace()
               && options.Password.IsNullOrWhiteSpace()
               && options.ServiceName.IsNotNullOrWhiteSpace()
               && options.Type.Equals("Kafka-static", StringComparison.OrdinalIgnoreCase);
    }

    public IKafkaAdminClientProvider Create(string connectionName)
    {
        var optionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<MessageQueueOptions>>();
        var config = optionsMonitor.Get(connectionName);

        return ActivatorUtilities.CreateInstance<KafkaAdminStaticClientProvider>(serviceProvider, connectionName, config.ToClientOptions());
    }
}
