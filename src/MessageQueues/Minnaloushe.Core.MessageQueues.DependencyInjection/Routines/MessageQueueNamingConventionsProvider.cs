using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.Abstractions.Routines;
using Minnaloushe.Core.MessageQueues.Routines;

namespace Minnaloushe.Core.MessageQueues.DependencyInjection.Routines;

public class MessageQueueNamingConventionsProvider : IMessageQueueNamingConventionsProvider
{
    public string GetTopicName<T>()
    {
        return MqNaming.GetSafeName<T>() ?? throw new InvalidOperationException("Type has no full name");
    }
    public string GetTopicName(Type messageType)
    {
        return MqNaming.GetSafeName(messageType) ?? throw new InvalidOperationException("Type has no full name");
    }
    /// <summary>
    /// Queue name for rabbitmq.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="options"></param>
    /// <returns></returns>
    public string GetServiceKey<T>(string serviceKey)
    {
        return $"{MqNaming.GetSafeName<T>()}.{serviceKey}".ToLower();
    }

    public string GetServiceKey<T>(MessageQueueOptions options)
    {
        return $"{MqNaming.GetSafeName<T>()}.{options.ServiceKey}".ToLower();
    }
    public string GetServiceKey(Type messageType, MessageQueueOptions options)
    {
        return $"{MqNaming.GetSafeName(messageType)}.{options.ServiceKey}".ToLower();
    }
    public string GetDeadLetterSuffix<T>() => "dlt";
    public string GetDeadLetterSuffix(Type messageType) => "dlt";
}