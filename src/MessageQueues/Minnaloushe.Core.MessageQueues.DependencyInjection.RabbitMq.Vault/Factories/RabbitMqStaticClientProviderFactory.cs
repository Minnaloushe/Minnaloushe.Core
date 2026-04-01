using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.ClientProviders.Abstractions;
using Minnaloushe.Core.ClientProviders.RabbitMq.Vault;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.RabbitMq.Factories;
using Minnaloushe.Core.Toolbox.StringExtensions;
using RabbitMQ.Client;

namespace Minnaloushe.Core.MessageQueues.DependencyInjection.RabbitMq.Vault.Factories;

internal class RabbitMqStaticClientProviderFactory(IServiceProvider serviceProvider) : IRabbitMqClientProviderFactory
{
    public bool CanCreate(MessageQueueOptions options)
    {
        return options.Username.IsNullOrWhiteSpace()
               && options.Password.IsNullOrWhiteSpace()

               && options.ServiceName.IsNotNullOrWhiteSpace()
               && options.Type.Equals("rabbit-static", StringComparison.OrdinalIgnoreCase);
    }

    public IClientProvider<IConnection> Create(string connectionName)
    {
        var optionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<MessageQueueOptions>>();
        var options = Options.Create(optionsMonitor.Get(connectionName));

        return ActivatorUtilities.CreateInstance<RabbitMqStaticClientProvider>(serviceProvider, options);
    }
}
