using Minnaloushe.Core.MessageQueues.Abstractions;

namespace Template.WebApi.Host.Consumer;

public class TestBrokenMessageConsumer : IConsumer<TestBrokenMessage>
{
    public Task<bool> HandleMessageAsync(MessageEnvelop<TestBrokenMessage> envelop, CancellationToken cancellationToken = default)
    {
        throw new Exception("Oops!");
    }
}