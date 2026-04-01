using Minnaloushe.Core.MessageQueues.Abstractions.ErrorHandling;

namespace Minnaloushe.Core.MessageQueues.Abstractions;

public interface IMessageEngineFactory<TMessage, in TProvider>
    where TProvider : class
{
    IMessageEngine CreateEngine(
        string name,
        TProvider provider,
        IConsumer<TMessage> consumer,
        MessageQueueOptions options,
        IErrorHandlingStrategy errorHandlingStrategy);
}