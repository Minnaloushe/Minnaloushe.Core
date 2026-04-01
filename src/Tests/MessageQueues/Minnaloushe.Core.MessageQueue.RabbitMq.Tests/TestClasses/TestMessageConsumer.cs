using Minnaloushe.Core.MessageQueues.Abstractions;

namespace Minnaloushe.Core.MessageQueue.RabbitMq.Tests.TestClasses;

public class TestMessageConsumer : IConsumer<TestMessage>
{
    public Task<bool> HandleMessageAsync(MessageEnvelop<TestMessage> envelop, CancellationToken cancellationToken = default)
    {
        Console.WriteLine(envelop.Message.Data);

        return Task.FromResult(true);
    }
}