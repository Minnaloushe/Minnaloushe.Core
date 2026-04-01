using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.Toolbox.Collections;

namespace Template.WebApi.Host.Consumer;

public class TestMessageConsumer : IConsumer<TestMessage>
{
    private readonly ConcurrentCircularBuffer<TestMessage> _buffer = new(10);
    public Task<bool> HandleMessageAsync(MessageEnvelop<TestMessage> envelop, CancellationToken cancellationToken = default)
    {
        Console.WriteLine(envelop.Message.Data);

        _buffer.Add(envelop.Message);

        return Task.FromResult(true);
    }

    public IReadOnlyCollection<TestMessage> GetBufferSnapshot() => [.. _buffer];
}