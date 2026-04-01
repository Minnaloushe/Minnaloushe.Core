using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.ClientProviders.Kafka;
using Minnaloushe.Core.ClientProviders.Kafka.Wrappers;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.Abstractions.Routines;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Extensions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Kafka.Factories;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Models;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Registries;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Routines;
using Minnaloushe.Core.MessageQueues.Kafka.Consumers.Factories;
using Moq;

namespace Minnaloushe.Core.MessageQueues.Kafka.Tests.Unit;

/// <summary>
/// Unit tests for consumer registration using AddConsumer extension method.
/// These are pure unit tests that verify registration logic without requiring infrastructure.
/// </summary>
[TestFixture]
[Category("Unit")]
public class ConsumerRegistrationTests
{
    #region Fixture members

    #region Constants

    private const string Consumer1Name = "test-consumer-1";
    private const string Consumer2Name = "test-consumer-2";
    private const string ConnectionName = "test-connection";

    #endregion

    #region Helper classes

    public class TestMessage1
    {
        public Guid Id { get; init; }
        public string Content { get; init; } = string.Empty;
    }

    public class TestMessage2
    {
        public Guid Id { get; init; }
        public string Data { get; init; } = string.Empty;
    }

    public class TestConsumer1 : IConsumer<TestMessage1>
    {
        public Task<bool> HandleMessageAsync(MessageEnvelop<TestMessage1> message, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }

    public class TestConsumer2 : IConsumer<TestMessage2>
    {
        public Task<bool> HandleMessageAsync(MessageEnvelop<TestMessage2> message, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }

    #endregion

    #endregion

    [Test]
    public void CreateRegistrar_WhenKafkaConsumerFactory_ThenShouldCreateValidRegistrar()
    {
        // Arrange
        var factory = new KafkaConsumerFactory();

        // Act
        var registrar1 = factory.CreateRegistrar(typeof(TestMessage1));
        var registrar2 = factory.CreateRegistrar(typeof(TestMessage2));

        // Assert
        registrar1.Should().NotBeNull();
        registrar2.Should().NotBeNull();
        registrar1.Should().NotBeSameAs(registrar2);
    }

    [Test]
    public void Register_WhenKafkaRegistrarCalled_ThenShouldRegisterAllRequiredServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Mock required dependencies - register with connection name as key
        var mockAdminProvider = new Mock<IKafkaAdminClientProvider>().Object;
        var mockKafkaConsumerProvider = new Mock<IKafkaConsumerClientProvider>().Object;
        var mockAdminProviderFactory = new Mock<IKafkaAdminClientProviderFactory>();
        mockAdminProviderFactory.Setup(x => x.Create(It.IsAny<string>()))
            .Returns(mockAdminProvider);


        services.AddKeyedSingleton(ConnectionName, mockAdminProvider);
        services.AddKeyedSingleton(ConnectionName, mockKafkaConsumerProvider);
        services.AddSingleton(Mock.Of<IMessageQueueNamingConventionsProvider>());
        services.AddSingleton(Mock.Of<IOptionsMonitor<MessageQueueOptions>>());
        services.AddScoped<IConsumer<TestMessage1>, TestConsumer1>();

        var factory = new KafkaConsumerFactory();
        var registrar = factory.CreateRegistrar(typeof(TestMessage1));

        // Act
        registrar.Register(services, Consumer1Name, ConnectionName, 1);
        var sp = services.BuildServiceProvider();

        // Assert
        // Verify initializer is registered as keyed service
        var initializer = sp.GetKeyedService<IConsumerInitializer>(Consumer1Name);
        initializer.Should().NotBeNull();

        // Verify message engine factory is registered
        var engineFactory = sp.GetService<IMessageEngineFactory<TestMessage1?, IKafkaConsumerClientWrapper>>();
        engineFactory.Should().NotBeNull();

        // Verify hosted service is registered
        var hostedServices = sp.GetServices<IHostedService>().ToList();
        var consumerHostedService = hostedServices.OfType<ConsumerHostedService<TestMessage1?, IKafkaConsumerClientWrapper>>().FirstOrDefault();
        consumerHostedService.Should().NotBeNull();
    }

    [Test]
    public void Register_WhenMultipleConsumersRegistered_ThenShouldHaveSeparateInitializers()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Mock required dependencies - register with connection name as key
        var mockAdminProvider = new Mock<IKafkaAdminClientProvider>().Object;
        var mockKafkaConsumerProvider1 = new Mock<IKafkaConsumerClientProvider>().Object;
        var mockKafkaConsumerProvider2 = new Mock<IKafkaConsumerClientProvider>().Object;

        services.AddKeyedSingleton(ConnectionName, mockAdminProvider);
        services.AddKeyedSingleton(ConnectionName, mockKafkaConsumerProvider1);
        services.AddKeyedSingleton(ConnectionName, mockKafkaConsumerProvider2);
        services.AddSingleton(Mock.Of<IMessageQueueNamingConventionsProvider>());
        services.AddSingleton(Mock.Of<IOptionsMonitor<MessageQueueOptions>>());
        services.AddScoped<IConsumer<TestMessage1>, TestConsumer1>();
        services.AddScoped<IConsumer<TestMessage2>, TestConsumer2>();

        var factory = new KafkaConsumerFactory();

        // Register two consumers
        var registrar1 = factory.CreateRegistrar(typeof(TestMessage1));
        var registrar2 = factory.CreateRegistrar(typeof(TestMessage2));

        // Act
        registrar1.Register(services, Consumer1Name, ConnectionName, 1);
        registrar2.Register(services, Consumer2Name, ConnectionName, 1);
        var sp = services.BuildServiceProvider();

        // Assert
        // Verify both initializers are registered
        var initializer1 = sp.GetKeyedService<IConsumerInitializer>(Consumer1Name);
        var initializer2 = sp.GetKeyedService<IConsumerInitializer>(Consumer2Name);

        initializer1.Should().NotBeNull();
        initializer2.Should().NotBeNull();
        initializer1.Should().NotBeSameAs(initializer2);
    }

    [Test]
    public void GetTopicName_WhenCalledForDifferentTypes_ThenShouldDifferentTopicNames()
    {
        // Arrange
        var routines = new MessageQueueNamingConventionsProvider();

        // Act
        var topicName1 = routines.GetTopicName<TestMessage1>();
        var topicName2 = routines.GetTopicName<TestMessage2>();

        // Assert
        topicName1.Should().NotBeNullOrEmpty();
        topicName2.Should().NotBeNullOrEmpty();
        topicName1.Should().NotBe(topicName2);
    }

    [Test]
    public void GetServiceKey_WhenCalled_ThenShouldIncludeMessageType()
    {
        // Arrange
        var routines = new MessageQueueNamingConventionsProvider();
        var options = new MessageQueueOptions { ServiceKey = "test-service" };

        // Act
        var serviceKey = routines.GetServiceKey<TestMessage1>(options);

        // Assert
        serviceKey.Should().NotBeNullOrEmpty();
        serviceKey.Should().Contain("testmessage1");
        serviceKey.Should().Contain(options.ServiceKey);
    }

    [Test]
    public void RegisterAndRetrieve_WhenConsumerFactoryRegistry_ThenShouldStoreAndRetrieveFactories()
    {
        // Arrange
        var registry = new ConsumerFactoryRegistry();
        var kafkaFactory = new KafkaConsumerFactory();

        // Act
        registry.RegisterFactory("kafka", kafkaFactory);
        var retrievedKafka = registry.GetFactory("kafka");

        // Assert
        retrievedKafka.Should().BeSameAs(kafkaFactory);
    }

    [Test]
    public void Register_WhenConsumerFactoryRegistryWithDuplicateType_ThenShouldThrow()
    {
        // Arrange
        var registry = new ConsumerFactoryRegistry();
        var factory1 = new KafkaConsumerFactory();
        var factory2 = new KafkaConsumerFactory();
        registry.RegisterFactory("kafka", factory1);

        // Act
        Action act = () => registry.RegisterFactory("kafka", factory2);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void GetFactory_WhenConsumerFactoryRegistryWithUnknownType_ThenShouldReturnNull()
    {
        // Arrange
        var registry = new ConsumerFactoryRegistry();

        // Act
        var result = registry.GetFactory("unknown");

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public void ConsumerRegistration_WhenCreated_ThenShouldStoreMessageType()
    {
        // Arrange & Act
        var registration = new ConsumerRegistration
        {
            MessageType = typeof(TestMessage1),
            Name = Consumer1Name
        };

        // Assert
        registration.MessageType.Should().Be(typeof(TestMessage1));
        registration.Name.Should().Be(Consumer1Name);
    }

    [Test]
    public void AddConsumerExtension_WhenCalled_ThenShouldReturnBuilder()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new MessageQueueBuilder(
            services,
            new MessageQueueHandlerRegistry(),
            new ConnectionTypeRegistry(),
            Mock.Of<IConfiguration>());

        // Act
        var result1 = builder.AddConsumer<TestMessage1, TestConsumer1>(Consumer1Name);
        var result2 = result1.AddConsumer<TestMessage2, TestConsumer2>(Consumer2Name);

        // Assert
        result1.Should().BeSameAs(builder);
        result2.Should().BeSameAs(builder);
    }

    [Test]
    public void ConsumerOptions_WhenCreated_ThenShouldHaveDefaultValues()
    {
        // Arrange & Act
        var options = new MessageQueueOptions();

        // Assert
        options.Parallelism.Should().Be(1);
        options.ConsumerLoopDelay.Should().Be(TimeSpan.FromSeconds(30));
        options.ConsumerErrorDelay.Should().Be(TimeSpan.FromSeconds(5));
    }
}
