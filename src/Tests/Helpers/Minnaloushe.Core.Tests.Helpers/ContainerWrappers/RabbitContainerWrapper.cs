using DotNet.Testcontainers.Builders;

namespace Minnaloushe.Core.Tests.Helpers.ContainerWrappers;

public class RabbitContainerWrapper : ContainerWrapperBase
{
    protected override ContainerBuilder InitContainer(ContainerBuilder builder)
    {
        return builder
            .WithEnvironment("RABBITMQ_DEFAULT_USER", Username)
            .WithEnvironment("RABBITMQ_DEFAULT_PASS", Password)
            .WithPortBinding(5672, true)
            .WithPortBinding(15672, true)
            // Wait for the AMQP port to become available instead of relying on a log line
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilMessageIsLogged("Server startup complete"));
    }

    protected override ushort ContainerPort => 5672;
    protected override string ImageName => "rabbitmq:4.2.5-management-alpine";
}