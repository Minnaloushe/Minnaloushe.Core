namespace Minnaloushe.Core.MessageQueues.Abstractions.Routines;

//TODO: Rework, make broker specific
public interface IMessageQueueNamingConventionsProvider
{
    string GetTopicName<T>();
    string GetTopicName(Type messageType);
    string GetServiceKey<T>(string options);
    string GetServiceKey<T>(MessageQueueOptions options);
    string GetServiceKey(Type messageType, MessageQueueOptions options);
    string GetDeadLetterSuffix<T>();
    string GetDeadLetterSuffix(Type messageType);
}