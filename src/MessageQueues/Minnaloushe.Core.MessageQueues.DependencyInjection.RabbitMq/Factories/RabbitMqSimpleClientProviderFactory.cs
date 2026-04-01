using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.ClientProviders.Abstractions;
using Minnaloushe.Core.ClientProviders.RabbitMq;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.RabbitMq.Extensions;
using Minnaloushe.Core.Toolbox.StringExtensions;
using RabbitMQ.Client;

namespace Minnaloushe.Core.MessageQueues.DependencyInjection.RabbitMq.Factories;

public class RabbitMqSimpleClientProviderFactory(
    IServiceProvider serviceProvider) : IRabbitMqClientProviderFactory
{
    public bool CanCreate(MessageQueueOptions options)
    {
        return options.Username.IsNotNullOrWhiteSpace()
               && options.Password.IsNotNullOrWhiteSpace()
               && (options.Type.Equals("RabbitMq", StringComparison.OrdinalIgnoreCase)
                || options.Type.Equals("Rabbit", StringComparison.OrdinalIgnoreCase));
    }

    public IClientProvider<IConnection> Create(string connectionName)
    {
        var options = serviceProvider.GetRequiredService<IOptionsMonitor<MessageQueueOptions>>();
        var config = options.Get(connectionName);
        return ActivatorUtilities.CreateInstance<RabbitMqSimpleClientProvider>(
            serviceProvider,
            connectionName,
            config.ToClientOptions());
    }
}