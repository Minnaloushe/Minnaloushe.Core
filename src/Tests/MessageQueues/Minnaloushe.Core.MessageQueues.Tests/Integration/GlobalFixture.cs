using DotNet.Testcontainers.Networks;
using Minnaloushe.Core.Tests.Helpers;
using Minnaloushe.Core.Tests.Helpers.ContainerWrappers;

namespace Minnaloushe.Core.MessageQueues.Tests.Integration;

[SetUpFixture]
public class GlobalFixture
{
    public static INetwork Network { get; private set; } = null!;
    public static RabbitContainerWrapper RabbitMq { get; private set; } = new();
    public static KafkaContainerWrapper Kafka { get; private set; } = new();

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        Network = await ContainerHelpers.CreateNetwork("message-queues-test-network");

        await Task.WhenAll(
            RabbitMq.InitAsync("rabbitmq", Network),
            Kafka.InitAsync("kafka", Network)
            );

    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await RabbitMq.DisposeAsync();
        await Kafka.DisposeAsync();
        await Network.DisposeAsync();
    }
}