namespace Minnaloushe.Core.MessageQueues.Abstractions;

public interface IConsumer<TMessage>
{
    Task<bool> HandleMessageAsync(MessageEnvelop<TMessage> envelop, CancellationToken cancellationToken = default);
}