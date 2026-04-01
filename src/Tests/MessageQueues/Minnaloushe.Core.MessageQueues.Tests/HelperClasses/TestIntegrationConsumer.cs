using Microsoft.Extensions.Logging;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.Tests.Integration;

namespace Minnaloushe.Core.MessageQueues.Tests.HelperClasses;

public class TestIntegrationConsumer(ILogger<TestIntegrationConsumer> logger) : IConsumer<TestMessage>
{
    public Task<bool> HandleMessageAsync(MessageEnvelop<TestMessage> envelop, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Received message: {Data}", envelop.Message.Data);
        MessageQueuesIntegrationTests.ReceivedMessages.Add(envelop.Message);
        return Task.FromResult(true);
    }
}