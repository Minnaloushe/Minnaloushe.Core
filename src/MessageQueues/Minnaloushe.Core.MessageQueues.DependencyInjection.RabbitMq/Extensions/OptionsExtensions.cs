using Minnaloushe.Core.ClientProviders.RabbitMq;
using Minnaloushe.Core.MessageQueues.Abstractions;

namespace Minnaloushe.Core.MessageQueues.DependencyInjection.RabbitMq.Extensions;

public static class OptionsExtensions
{
    public static RabbitMqClientOptions ToClientOptions(this MessageQueueOptions messageQueueOptions)
    {
        return new RabbitMqClientOptions
        {
            Host = messageQueueOptions.Host,
            Port = messageQueueOptions.Port,
            ServiceName = messageQueueOptions.ServiceName,
            Username = messageQueueOptions.Username,
            Password = messageQueueOptions.Password
        };
    }

}