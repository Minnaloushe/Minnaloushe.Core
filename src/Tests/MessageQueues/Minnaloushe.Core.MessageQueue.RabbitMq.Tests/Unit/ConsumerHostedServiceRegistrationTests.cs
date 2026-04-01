using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Extensions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.RabbitMq.Extensions;

namespace Minnaloushe.Core.MessageQueue.RabbitMq.Tests.Unit;

/// <summary>
/// Integration test to verify that ConsumerHostedService is properly registered and can be resolved.
/// </summary>
[TestFixture]
[Category("Unit")]
public class ConsumerHostedServiceRegistrationTests
{
    #region Helper classes

    [UsedImplicitly]
    private class TestMessage
    {
        public Guid Id { get; init; }
    }

    [UsedImplicitly]
    private class TestConsumer : IConsumer<TestMessage>
    {
        public Task<bool> HandleMessageAsync(MessageEnvelop<TestMessage> envelop, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }

    #endregion

    [Test]
    public void AddConsumer_WhenRegistered_ThenHostedServiceShouldBeResolvable()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MessageQueues:Connections:0:Name"] = "test-connection",
                ["MessageQueues:Connections:0:Type"] = "rabbitmq",
                ["MessageQueues:Connections:0:Host"] = "localhost",
                ["MessageQueues:Connections:0:Port"] = "5672",
                ["MessageQueues:Connections:0:Username"] = "guest",
                ["MessageQueues:Connections:0:Password"] = "guest",
                ["MessageQueues:Consumers:0:Name"] = "TestConsumer",
                ["MessageQueues:Consumers:0:ConnectionName"] = "test-connection",
                ["MessageQueues:Consumers:0:Parallelism"] = "2"
            })
            .Build();

        services.AddLogging();

        // Act
        services.AddMessageQueues(configuration)
            .AddRabbitMqClientProviders()
            .AddRabbitMqConsumers()
            .AddConsumer<TestMessage, TestConsumer>("TestConsumer")
            .Build();

        var serviceProvider = services.BuildServiceProvider();

        // Assert - Verify hosted services are registered
        var hostedServices = serviceProvider.GetServices<IHostedService>().ToList();
        Assert.That(hostedServices, Is.Not.Empty, "At least one hosted service should be registered");

        var consumerHostedService = hostedServices
            .OfType<ConsumerHostedService<TestMessage, RabbitMQ.Client.IConnection>>()
            .FirstOrDefault();

        Assert.That(consumerHostedService, Is.Not.Null,
            "ConsumerHostedService should be registered for TestMessage");
    }

    [Test]
    public void AddConsumer_WhenConsumerRegistered_ThenInitializerShouldBeRegistered()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MessageQueues:Connections:0:Name"] = "test-connection",
                ["MessageQueues:Connections:0:Type"] = "rabbitmq",
                ["MessageQueues:Connections:0:Host"] = "localhost",
                ["MessageQueues:Connections:0:Port"] = "5672",
                ["MessageQueues:Consumers:0:Name"] = "TestConsumer",
                ["MessageQueues:Consumers:0:ConnectionName"] = "test-connection"
            })
            .Build();

        services.AddLogging();

        // Act
        services.AddMessageQueues(configuration)
            .AddRabbitMqClientProviders()
            .AddRabbitMqConsumers()
            .AddConsumer<TestMessage, TestConsumer>("TestConsumer")
            .Build();

        // Assert - Check that initializer factory is registered (without resolving it which would require full dependencies)
        var initializerDescriptor = services.FirstOrDefault(sd =>
            sd.ServiceType == typeof(IConsumerInitializer) &&
            sd.ServiceKey?.ToString() == "TestConsumer");

        Assert.That(initializerDescriptor, Is.Not.Null,
            "IConsumerInitializer should be registered as keyed service with key 'TestConsumer'");
    }

    [Test]
    public void AddConsumer_WhenConsumerRegistered_ThenEngineFactoryShouldBeRegistered()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MessageQueues:Connections:0:Name"] = "test-connection",
                ["MessageQueues:Connections:0:Type"] = "rabbitmq",
                ["MessageQueues:Connections:0:Host"] = "localhost",
                ["MessageQueues:Connections:0:Port"] = "5672",
                ["MessageQueues:Consumers:0:Name"] = "TestConsumer",
                ["MessageQueues:Consumers:0:ConnectionName"] = "test-connection"
            })
            .Build();

        services.AddLogging();

        // Act
        services.AddMessageQueues(configuration)
            .AddRabbitMqClientProviders()
            .AddRabbitMqConsumers()
            .AddConsumer<TestMessage, TestConsumer>("TestConsumer")
            .Build();

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var engineFactory = serviceProvider.GetService<IMessageEngineFactory<TestMessage, RabbitMQ.Client.IConnection>>();
        Assert.That(engineFactory, Is.Not.Null, "IMessageEngineFactory should be registered");
    }
}
