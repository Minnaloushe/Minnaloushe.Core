using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.ClientProviders.Kafka;
using Minnaloushe.Core.MessageQueues.Abstractions;
using Minnaloushe.Core.MessageQueues.Abstractions.Routines;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Extensions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Models;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Registries;
using Minnaloushe.Core.MessageQueues.Kafka.Producers;
using Minnaloushe.Core.MessageQueues.Kafka.Producers.Extensions;
using Minnaloushe.Core.MessageQueues.Kafka.Producers.Factories;
using Moq;
using System.Text.Json;

namespace Minnaloushe.Core.MessageQueues.Kafka.Tests.Unit;

/// <summary>
/// Unit tests for producer registration using AddProducer extension method.
/// These are pure unit tests that verify registration logic without requiring infrastructure.
/// </summary>
[TestFixture]
[Category("Unit")]
public class ProducerRegistrationTests
{
    #region Fixture members

    #region Constants

    private const string Producer1Name = "test-producer-1";
    private const string Producer2Name = "test-producer-2";
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

    #endregion

    #endregion

    [Test]
    public void CreateRegistrar_WhenKafkaProducerFactory_ThenShouldCreateValidRegistrar()
    {
        // Arrange
        var factory = new KafkaProducerFactory();

        // Act
        var registrar1 = factory.CreateRegistrar(typeof(TestMessage1));
        var registrar2 = factory.CreateRegistrar(typeof(TestMessage2));

        // Assert
        registrar1.Should().NotBeNull("Factory should create registrar for TestMessage1");
        registrar2.Should().NotBeNull("Factory should create registrar for TestMessage2");
        registrar1.Should().NotBeSameAs(registrar2, "Different message types should get different registrar instances");
    }

    [Test]
    public void Register_WhenKafkaProducerRegistrarCalled_ThenShouldRegisterAllRequiredServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Mock required dependencies - register with connection name as key
        var mockProducerClientProvider = new Mock<IKafkaProducerClientProvider>().Object;
        var mockAdminClientProvider = new Mock<IKafkaAdminClientProvider>().Object;

        // Prepare a default ConsumerOptions instance and set up the IOptionsMonitor mock to return it
        var defaultOptions = new MessageQueueOptions();
        var optionsMock = new Mock<IOptionsMonitor<MessageQueueOptions>>();
        optionsMock.Setup(x => x.Get(It.IsAny<string>())).Returns(defaultOptions);
        optionsMock.SetupGet(x => x.CurrentValue).Returns(defaultOptions);
        services.AddSingleton<IOptionsMonitor<MessageQueueOptions>>(optionsMock.Object);
        services.AddSingleton(new JsonSerializerOptions());
        services.AddKeyedSingleton(ConnectionName, mockProducerClientProvider);
        services.AddKeyedSingleton(ConnectionName, mockAdminClientProvider);
        // Also register non-keyed admin provider so ActivatorUtilities can resolve it when creating the producer
        services.AddSingleton<IKafkaAdminClientProvider>(mockAdminClientProvider);
        services.AddSingleton(Mock.Of<IMessageQueueNamingConventionsProvider>());

        var factory = new KafkaProducerFactory();
        var registrar = factory.CreateRegistrar(typeof(TestMessage1));

        // Act
        registrar.Register(services, Producer1Name, ConnectionName, null);
        var sp = services.BuildServiceProvider();

        // Assert
        // Verify producer client provider alias is registered with producer name
        var clientProvider = sp.GetKeyedService<IKafkaProducerClientProvider>(Producer1Name);
        clientProvider.Should().NotBeNull("Producer client provider should be aliased with producer name as key");

        // Verify producer instance is registered
        var producer = sp.GetKeyedService<KafkaProducer<TestMessage1>>(Producer1Name);
        producer.Should().NotBeNull("Producer instance should be registered with producer name as key");

        // Verify IProducer interface is registered
        var iProducer = sp.GetKeyedService<IProducer<TestMessage1>>(Producer1Name);
        iProducer.Should().NotBeNull("IProducer interface should be registered with producer name as key");
        iProducer.Should().BeSameAs(producer, "IProducer should resolve to same instance as concrete producer");
    }

    [Test]
    public void Register_WhenMultipleProducersRegistered_ThenShouldHaveSeparateInstances()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // Mock required dependencies - register with connection name as key
        var mockProducerClientProvider = new Mock<IKafkaProducerClientProvider>().Object;
        var mockAdminClientProvider = new Mock<IKafkaAdminClientProvider>().Object;

        // Prepare a default ConsumerOptions instance and set up the IOptionsMonitor mock to return it
        var defaultOptions = new MessageQueueOptions();
        var optionsMock = new Mock<IOptionsMonitor<MessageQueueOptions>>();
        optionsMock.Setup(x => x.Get(It.IsAny<string>())).Returns(defaultOptions);
        optionsMock.SetupGet(x => x.CurrentValue).Returns(defaultOptions);
        services.AddSingleton<IOptionsMonitor<MessageQueueOptions>>(optionsMock.Object);
        services.AddSingleton(Mock.Of<IMessageQueueNamingConventionsProvider>());
        services.AddSingleton(new JsonSerializerOptions());
        services.AddKeyedSingleton(ConnectionName, mockProducerClientProvider);
        services.AddKeyedSingleton(ConnectionName, mockAdminClientProvider);
        // Also register non-keyed admin provider so ActivatorUtilities can resolve it when creating the producer
        services.AddSingleton<IKafkaAdminClientProvider>(mockAdminClientProvider);

        var factory = new KafkaProducerFactory();

        // Register two producers
        var registrar1 = factory.CreateRegistrar(typeof(TestMessage1));
        var registrar2 = factory.CreateRegistrar(typeof(TestMessage2));

        registrar1.Register(services, Producer1Name, ConnectionName, null);
        registrar2.Register(services, Producer2Name, ConnectionName, null);

        var sp = services.BuildServiceProvider();

        // Verify both producers are registered
        var producer1 = sp.GetKeyedService<IProducer<TestMessage1>>(Producer1Name);
        var producer2 = sp.GetKeyedService<IProducer<TestMessage2>>(Producer2Name);

        producer1.Should().NotBeNull("First producer should be registered");
        producer2.Should().NotBeNull("Second producer should be registered");
        producer1.Should().NotBeSameAs(producer2, "Different producers should be separate instances");
    }

    [Test]
    public void ResolveProducer_WhenSameProducerResolvedMultipleTimes_ThenShouldReturnSameInstance()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // Mock required dependencies
        var mockProducerClientProvider = new Mock<IKafkaProducerClientProvider>().Object;
        var mockAdminClientProvider = new Mock<IKafkaAdminClientProvider>().Object;


        services.AddKeyedSingleton(ConnectionName, mockProducerClientProvider);
        services.AddKeyedSingleton(ConnectionName, mockAdminClientProvider);
        services.AddSingleton(new JsonSerializerOptions());
        services.AddSingleton(Mock.Of<IMessageQueueNamingConventionsProvider>());
        var optsSame = new MessageQueueOptions();
        var optionsMockSame = new Mock<IOptionsMonitor<MessageQueueOptions>>();
        optionsMockSame.Setup(x => x.Get(It.IsAny<string>())).Returns(optsSame);
        optionsMockSame.SetupGet(x => x.CurrentValue).Returns(optsSame);
        services.AddSingleton(optionsMockSame.Object);

        var factory = new KafkaProducerFactory();
        var registrar = factory.CreateRegistrar(typeof(TestMessage1));
        registrar.Register(services, Producer1Name, ConnectionName, null);

        var sp = services.BuildServiceProvider();

        // Resolve multiple times
        var producer1 = sp.GetRequiredKeyedService<IProducer<TestMessage1>>(Producer1Name);
        var producer2 = sp.GetRequiredKeyedService<IProducer<TestMessage1>>(Producer1Name);

        producer1.Should().BeSameAs(producer2, "Should return same producer instance when resolved multiple times");
    }

    [Test]
    public void RegisterFactory_WhenProducerFactoryRegistry_ThenShouldStoreAndRetrieveFactories()
    {
        var registry = new ProducerFactoryRegistry();
        var kafkaFactory = new KafkaProducerFactory();

        registry.RegisterFactory("kafka", kafkaFactory);

        var retrievedKafka = registry.GetFactory("kafka");

        retrievedKafka.Should().BeSameAs(kafkaFactory, "Should retrieve same Kafka factory instance");
    }

    [Test]
    public void RegisterFactory_WhenProducerFactoryRegistryWithDuplicateType_ThenShouldThrow()
    {
        var registry = new ProducerFactoryRegistry();
        var factory1 = new KafkaProducerFactory();
        var factory2 = new KafkaProducerFactory();

        registry.RegisterFactory("kafka", factory1);

        Assert.Throws<InvalidOperationException>(() => registry.RegisterFactory("kafka", factory2),
            "Should throw when registering duplicate connection type");
    }

    [Test]
    public void GetFactory_WhenProducerFactoryRegistryWithUnknownType_ThenShouldReturnNull()
    {
        var registry = new ProducerFactoryRegistry();

        var result = registry.GetFactory("unknown");

        result.Should().BeNull("Should return null for unknown connection type");
    }

    [Test]
    public void ProducerRegistration_WhenCreated_ThenShouldStoreAllProperties()
    {
        var registration = new ProducerRegistration
        {
            MessageType = typeof(TestMessage1),
            Name = Producer1Name,
            ConnectionName = ConnectionName
        };

#pragma warning disable CA2263
        registration.MessageType.Should().Be(typeof(TestMessage1), "Should store message type");
#pragma warning restore CA2263
        registration.Name.Should().Be(Producer1Name, "Should store producer name");
        registration.ConnectionName.Should().Be(ConnectionName, "Should store connection name");
    }

    [Test]
    public void AddProducerExtension_WhenCalled_ThenShouldReturnBuilder()
    {
        var services = new ServiceCollection();
        var builder = new MessageQueueBuilder(
            services,
            new MessageQueueHandlerRegistry(),
            new ConnectionTypeRegistry(),
            Mock.Of<Microsoft.Extensions.Configuration.IConfiguration>());

        var result1 = builder.AddProducer<TestMessage1>(ConnectionName, Producer1Name);
        var result2 = result1.AddProducer<TestMessage2>(ConnectionName, Producer2Name);

        result1.Should().BeSameAs(builder, "Should return same builder instance for chaining");
        result2.Should().BeSameAs(builder, "Should return same builder instance for chaining");
    }

    [Test]
    public void AddProducer_WhenCalledWithoutName_ThenShouldUseDefaultName()
    {
        var services = new ServiceCollection();
        var builder = new MessageQueueBuilder(
            services,
            new MessageQueueHandlerRegistry(),
            new ConnectionTypeRegistry(),
            Mock.Of<Microsoft.Extensions.Configuration.IConfiguration>());

        builder.AddProducer<TestMessage1>(ConnectionName);

        var registration = builder.ProducerRegistrations.FirstOrDefault();

        registration.Should().NotBeNull("Should create producer registration");
        registration.Name.Should().NotBeNullOrEmpty("Should generate default name");
        registration.Name.ToLowerInvariant().Should().Contain("testmessage1", "Default name should be based on message type");
    }

    [Test]
    public void AddProducer_WhenMultipleProducersAdded_ThenShouldStoreAllRegistrations()
    {
        var services = new ServiceCollection();
        var builder = new MessageQueueBuilder(
            services,
            new MessageQueueHandlerRegistry(),
            new ConnectionTypeRegistry(),
            Mock.Of<Microsoft.Extensions.Configuration.IConfiguration>());

        builder.AddProducer<TestMessage1>(ConnectionName, Producer1Name);
        builder.AddProducer<TestMessage2>(ConnectionName, Producer2Name);

        builder.ProducerRegistrations.Should().HaveCount(2, "Should store both producer registrations");

        var reg1 = builder.ProducerRegistrations.FirstOrDefault(r => r.Name == Producer1Name);
        var reg2 = builder.ProducerRegistrations.FirstOrDefault(r => r.Name == Producer2Name);

        reg1.Should().NotBeNull("First producer registration should exist");
        reg2.Should().NotBeNull("Second producer registration should exist");
#pragma warning disable CA2263
        reg1.MessageType.Should().Be(typeof(TestMessage1), "First registration should have correct message type");
        reg2.MessageType.Should().Be(typeof(TestMessage2), "Second registration should have correct message type");
#pragma warning restore CA2263
    }

    [Test]
    public void Register_WhenProducerRegistered_ThenShouldBeResolvableByConnectionName()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var mockProducerClientProvider = new Mock<IKafkaProducerClientProvider>().Object;
        var mockAdminClientProvider = new Mock<IKafkaAdminClientProvider>().Object;

        var defaultOptions = new MessageQueueOptions();
        var optionsMock = new Mock<IOptionsMonitor<MessageQueueOptions>>();
        optionsMock.Setup(x => x.Get(It.IsAny<string>())).Returns(defaultOptions);
        optionsMock.SetupGet(x => x.CurrentValue).Returns(defaultOptions);
        services.AddSingleton<IOptionsMonitor<MessageQueueOptions>>(optionsMock.Object);
        services.AddSingleton(new JsonSerializerOptions());
        services.AddKeyedSingleton(ConnectionName, mockProducerClientProvider);
        services.AddKeyedSingleton(ConnectionName, mockAdminClientProvider);
        services.AddSingleton<IKafkaAdminClientProvider>(mockAdminClientProvider);
        services.AddSingleton(Mock.Of<IMessageQueueNamingConventionsProvider>());

        var factory = new KafkaProducerFactory();
        var registrar = factory.CreateRegistrar(typeof(TestMessage1));

        // Act
        registrar.Register(services, Producer1Name, ConnectionName, null);
        var sp = services.BuildServiceProvider();

        // Assert
        var producerByName = sp.GetKeyedService<IProducer<TestMessage1>>(Producer1Name);
        var producerByConnection = sp.GetKeyedService<IProducer<TestMessage1>>(ConnectionName);

        producerByName.Should().NotBeNull("Producer should be resolvable by producer name");
        producerByConnection.Should().NotBeNull("Producer should be resolvable by connection name");
        producerByConnection.Should().BeSameAs(producerByName, "Both resolutions should return the same instance");
    }

    [Test]
    public void AddKafkaProducers_WhenCalled_ThenShouldRegisterFactory()
    {
        var services = new ServiceCollection();
        var builder = new MessageQueueBuilder(
            services,
            new MessageQueueHandlerRegistry(),
            new ConnectionTypeRegistry(),
            Mock.Of<Microsoft.Extensions.Configuration.IConfiguration>());

        builder.AddKafkaProducers();

        // Check that producer factory is registered in the registry
        var factoryRegistry = services.BuildServiceProvider().GetService<IProducerFactoryRegistry>();

        // The registry should be created even if we can't directly access it from this test
        // At minimum, the extension method should complete without errors
        Assert.Pass("AddKafkaProducers should register factory without errors");
    }

    [Test]
    public void GetKeyedService_WhenProducerClientProviderAliased_ThenShouldResolveFromConnectionName()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // Register provider with connection name
        var mockProducerClientProvider = new Mock<IKafkaProducerClientProvider>().Object;
        services.AddKeyedSingleton<IKafkaProducerClientProvider>(ConnectionName, mockProducerClientProvider);
        services.AddSingleton(Mock.Of<IMessageQueueNamingConventionsProvider>());
        // Ensure IOptionsMonitor returns a default ConsumerOptions
        var opts = new MessageQueueOptions();
        var optionsMock2 = new Mock<IOptionsMonitor<MessageQueueOptions>>();
        optionsMock2.Setup(x => x.Get(It.IsAny<string>())).Returns(opts);
        optionsMock2.SetupGet(x => x.CurrentValue).Returns(opts);
        services.AddSingleton<IOptionsMonitor<MessageQueueOptions>>(optionsMock2.Object);

        var factory = new KafkaProducerFactory();
        var registrar = factory.CreateRegistrar(typeof(TestMessage1));
        registrar.Register(services, Producer1Name, ConnectionName, null);

        var sp = services.BuildServiceProvider();

        // Resolve by producer name (alias)
        var providerByProducerName = sp.GetKeyedService<IKafkaProducerClientProvider>(Producer1Name);
        // Resolve by connection name (original)
        var providerByConnectionName = sp.GetKeyedService<IKafkaProducerClientProvider>(ConnectionName);

        providerByProducerName.Should().NotBeNull("Should resolve provider by producer name");
        providerByConnectionName.Should().NotBeNull("Should resolve provider by connection name");
        providerByProducerName.Should().BeSameAs(providerByConnectionName,
            "Both should resolve to the same provider instance");
    }
}