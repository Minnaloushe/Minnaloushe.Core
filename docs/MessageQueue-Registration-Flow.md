# Message Queue Producer and Consumer Registration Flow

## Overview

The message queue system uses a factory-based registration pattern that supports multiple queue providers (RabbitMQ, Kafka) with a unified API. This document explains the registration flow for both consumers and producers.

## Architecture Components

### Core Abstractions

1. **`IConsumer<TMessage>`** - Interface for message consumers
2. **`IProducer<TMessage>`** - Interface for message producers
3. **`MessageQueueBuilder`** - Fluent builder for configuring message queues
4. **`ConsumerOptions`** - Configuration options for connections and consumers

### Factory Pattern Components

#### Consumer Registration
- **`IConsumerFactory`** - Factory interface for creating consumer registrars
- **`IConsumerRegistrar`** - Type-safe registrar for registering consumer services
- **`IConsumerFactoryRegistry`** - Registry mapping connection types to factories
- **`ConsumerRegistration`** - Record storing consumer registration metadata

#### Producer Registration
- **`IProducerFactory`** - Factory interface for creating producer registrars
- **`IProducerRegistrar`** - Type-safe registrar for registering producer services
- **`IProducerFactoryRegistry`** - Registry mapping connection types to factories
- **`ProducerRegistration`** - Record storing producer registration metadata

### Provider-Specific Components

#### Kafka
- **`KafkaConsumerFactory`** / **`KafkaProducerFactory`** - Creates registrars for Kafka
- **`KafkaConsumerClientWrapper`** - Wraps `IConsumer<byte[], byte[]>` from Confluent.Kafka
- **`KafkaProducerClientWrapper`** - Wraps `IProducer<byte[], byte[]>` from Confluent.Kafka
- **Serialization** - Manual JSON serialization/deserialization at consumer/producer level

#### RabbitMQ
- **`RabbitMqConsumerFactory`** / **`RabbitMqProducerFactory`** - Creates registrars for RabbitMQ
- **`IConnection`** / **`IChannel`** - RabbitMQ client types

---

## Consumer Registration Flow

### 1. Initialization Phase

```csharp
services.AddMessageQueues(configuration)
    .AddRabbitMqClientProviders()      // Register RabbitMQ client providers
    .AddRabbitMqConsumers()            // Register RabbitMQ consumer factory
    .AddKafkaClientProviders()         // Register Kafka client providers  
    .AddKafkaConsumers()               // Register Kafka consumer factory
    .AddConsumer<MyMessage, MyConsumer>("my-consumer")  // Register consumer
    .Build();                          // Finalize registration
```

**What happens:**
1. `AddMessageQueues()` creates `MessageQueueBuilder` and registers `IMessageQueueRoutines`
2. Provider-specific methods register:
   - Client provider factories and selectors
   - Message queue handlers for connection types
3. `AddConsumer<TMessage, TConsumer>()` stores `ConsumerRegistration` in builder
4. `Build()` processes all registrations

### 2. Build Phase (`MessageQueueConfigurationExtensions.Build()`)

```
┌─────────────────────────────────────────────────────────────┐
│ 1. Parse Configuration                                      │
│    - Load MessageQueues:Connections                         │
│    - Load MessageQueues:Consumers                           │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ 2. Merge Consumer Definitions                               │
│    - Config-defined consumers                               │
│    - Programmatically registered consumers                  │
│    - Validate each has configuration                        │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ 3. Register Consumer Options                                │
│    - Bind connection config to ConsumerOptions(name)        │
│    - Bind consumer config to ConsumerOptions(name)          │
│    - Validate options on start                              │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ 4. Register Connection Initializers                         │
│    - Group consumers by connection                          │
│    - For each connection:                                   │
│      • Bind ConsumerOptions(connectionName)                 │
│      • Invoke connection type handler                       │
│      • Register client providers (keyed by connection name) │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ 5. Register Consumer Services                               │
│    - Get factory from ConsumerFactoryRegistry by type       │
│    - For each consumer registration:                        │
│      • Create registrar from factory                        │
│      • Call registrar.Register()                            │
└─────────────────────────────────────────────────────────────┘
```

### 3. Consumer Service Registration (Per Consumer)

**Example: Kafka Consumer Registration**

```
KafkaConsumerRegistrar<TMessage>.Register(services, consumerName, connectionName)
│
├─ Register IKafkaAdminClientProvider (consumerName -> connectionName)
│  └─ Alias from consumer name to connection name
│
├─ Register IKafkaConsumerClientProvider (consumerName -> connectionName)
│  └─ Alias from consumer name to connection name
│
├─ Register IConsumerInitializer (keyed by consumerName)
│  └─ Creates: KafkaConsumerInitializer<TMessage>
│     - Creates Kafka topics if needed
│     - Uses admin client provider
│
├─ Register IMessageEngineFactory<TMessage?, IConsumer<byte[], byte[]>>
│  └─ Creates: KafkaMessageEngineFactory<TMessage>
│     - Factory for creating message engines
│
└─ Register ConsumerHostedService<TMessage?, IConsumer<byte[], byte[]>>
   └─ Background service that:
      - Initializes consumer (calls IConsumerInitializer)
      - Creates workers based on Parallelism setting
      - Each worker runs message engine
```

### 4. Runtime Flow (Per Consumer Worker)

```
ConsumerHostedService.StartAsync()
│
├─ Call IConsumerInitializer.InitializeAsync()
│  └─ Kafka: Create topic if doesn't exist
│  └─ RabbitMQ: Declare exchange and queue
│
├─ Create Workers (based on Parallelism setting)
│  └─ For each worker:
│     └─ Create IMessageEngine from factory
│
└─ Start Workers
   └─ Each worker runs:
      ┌─ Loop:
      │  ├─ IMessageEngine.ReceiveAsync()
      │  │  └─ Kafka: consumer.Consume() -> Deserialize from byte[]
      │  │  └─ RabbitMQ: channel.BasicGet() -> Deserialize from byte[]
      │  │
      │  ├─ IConsumer<TMessage>.HandleMessageAsync(message)
      │  │  └─ User's message handler
      │  │
      │  ├─ On Success: IMessageContext.AckAsync()
      │  │  └─ Kafka: Commit offset
      │  │  └─ RabbitMQ: BasicAck
      │  │
      │  └─ On Failure: IMessageContext.NackAsync()
      │     └─ Kafka: Send to DLQ (TODO)
      │     └─ RabbitMQ: BasicNack/Requeue
      └─ Repeat
```

---

## Producer Registration Flow

### 1. Initialization Phase

```csharp
services.AddMessageQueues(configuration)
    .AddRabbitMqClientProviders()      // Register RabbitMQ client providers
    .AddRabbitMqProducers()            // Register RabbitMQ producer factory
    .AddKafkaProducers()               // Register Kafka producer factory
    .AddProducer<MyMessage>("my-connection")  // Register producer
    .Build();                          // Finalize registration
```

**What happens:**
1. `AddRabbitMqProducers()` / `AddKafkaProducers()` register producer factories
2. `AddProducer<TMessage>(connectionName)` stores `ProducerRegistration` in builder
3. `Build()` processes all registrations

### 2. Build Phase - Producer Registration

```
┌─────────────────────────────────────────────────────────────┐
│ 1. Connection Handler Registration                          │
│    - Each connection type handler is invoked                │
│    - Registers client providers keyed by connection name    │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ 2. Register Producer Services                               │
│    - Get factory from ProducerFactoryRegistry by type       │
│    - For each producer registration:                        │
│      • Create registrar from factory                        │
│      • Call registrar.Register()                            │
└─────────────────────────────────────────────────────────────┘
```

### 3. Producer Service Registration (Per Producer)

**Example: Kafka Producer Registration**

```
KafkaProducerRegistrar<TMessage>.Register(services, producerName, connectionName)
│
├─ Register IKafkaProducerClientProvider (producerName -> connectionName)
│  └─ Alias from producer name to connection name
│
├─ Register KafkaProducer<TMessage> (keyed by producerName and connectionName)
│  └─ Concrete producer implementation
│     - Serializes messages to byte[]
│     - Publishes to Kafka topic
│
└─ Register IProducer<TMessage> (keyed by producerName)
   └─ Resolves to KafkaProducer<TMessage>
```

### 4. Runtime Flow (Publishing)

```
IProducer<TMessage>.PublishAsync(message, ct)
│
├─ Acquire client provider
│  └─ Get IKafkaProducerClientProvider by producer name
│     └─ Resolves to connection's client provider
│
├─ Get IProducer<byte[], byte[]> from wrapper
│
├─ Serialize message
│  └─ Kafka: JsonSerializer.SerializeToUtf8Bytes(message)
│  └─ RabbitMQ: JsonSerializer with RecyclableMemoryStream
│
├─ Create message envelope
│  └─ Kafka: Message<byte[], byte[]> { Key, Value }
│     - Key: Guid.NewGuid().ToString() as byte[]
│     - Value: serialized message
│  └─ RabbitMQ: BasicProperties + byte[]
│
└─ Publish
   └─ Kafka: producer.ProduceAsync(topic, message)
   └─ RabbitMQ: channel.BasicPublishAsync(exchange, routingKey, properties, payload)
```

---

## Key Design Patterns

### 1. Factory Pattern

**Problem:** Different queue providers need different registration logic.

**Solution:** Each provider implements `IConsumerFactory` / `IProducerFactory` that creates type-safe registrars.

```csharp
public interface IConsumerFactory
{
    IConsumerRegistrar CreateRegistrar(Type messageType);
}

// Kafka implementation
public class KafkaConsumerFactory : IConsumerFactory
{
    public IConsumerRegistrar CreateRegistrar(Type messageType)
    {
        // Creates KafkaConsumerRegistrar<TMessage>
        var registrarType = typeof(KafkaConsumerRegistrar<>)
            .MakeGenericType(messageType);
        return (IConsumerRegistrar)Activator.CreateInstance(registrarType)!;
    }
}
```

### 2. Registry Pattern

**Problem:** Multiple providers need to register themselves for specific connection types.

**Solution:** Central registries map connection types to factories.

```csharp
var registry = new ConsumerFactoryRegistry();
registry.RegisterFactory("kafka", new KafkaConsumerFactory());
registry.RegisterFactory("rabbitmq", new RabbitMqConsumerFactory());

// Later...
var factory = registry.GetFactory(connectionType); // From config
```

### 3. Keyed Services Pattern

**Problem:** Multiple instances of same type needed (multiple consumers/producers).

**Solution:** Register services with keys for lookup.

```csharp
// Register with connection name
services.AddKeyedSingleton<IKafkaProducerClientProvider>(
    connectionName, 
    provider);

// Alias with producer name
services.AddKeyedSingleton<IKafkaProducerClientProvider>(
    producerName,
    (sp, key) => sp.GetRequiredKeyedService<IKafkaProducerClientProvider>(connectionName));
```

### 4. Message Queue Handler Pattern

**Problem:** Provider-specific registration logic needs to be extensible.

**Solution:** Handlers registered per connection type.

```csharp
builder.Services.RegisterMessageQueueHandler("kafka", context =>
{
    // Register Kafka-specific providers
    context.RegisterKeyedProvider<
        IKafkaConsumerClientProvider,
        IKafkaConsumerClientProviderFactory,
        IKafkaConsumerClientProviderFactorySelector>();
});
```

---

## Configuration Structure

### appsettings.json Example

```json
{
  "MessageQueues": {
    "Connections": [
      {
        "Name": "kafka-connection",
        "Type": "kafka",
        "Host": "localhost",
        "Port": 9092,
        "ServiceKey": "my-service",
        "Username": "user",
        "Password": "pass"
      },
      {
        "Name": "rabbitmq-connection",
        "Type": "rabbitmq",
        "Host": "localhost",
        "Port": 5672,
        "Username": "guest",
        "Password": "guest"
      }
    ],
    "Consumers": [
      {
        "Name": "my-consumer",
        "ConnectionName": "kafka-connection",
        "Parallelism": 2
      }
    ]
  }
}
```

### Connection Name Resolution

```
Configuration Consumer Name: "my-consumer"
                    ↓
ConsumerOptions("my-consumer") - Merged from:
                    ├─ Connection config (kafka-connection)
                    └─ Consumer config (my-consumer)
                    ↓
Consumer Service Registration:
                    ├─ IConsumerInitializer (keyed: "my-consumer")
                    ├─ Client Providers (keyed: "my-consumer" -> "kafka-connection")
                    └─ ConsumerHostedService (uses "my-consumer" key)
```

---

## Kafka-Specific: Byte Array Serialization

### Why byte[] Instead of Generic Types?

**Old Approach (Generic):**
```csharp
IConsumer<string, TMessage> consumer;
// Serialization baked into wrapper
```

**New Approach (byte[]):**
```csharp
IConsumer<byte[], byte[]> consumer;
// Manual serialization in message engine/producer
```

**Benefits:**
1. **Flexibility** - Can use different serialization strategies
2. **Simplicity** - No generic type parameters in registration
3. **Separation** - Transport layer (byte[]) vs application layer (TMessage)

### Serialization Points

**Consumer (Deserialization):**
```csharp
// KafkaMessageEngine.ReceiveAsync()
var result = provider.Consume(ct);  // byte[]
var message = JsonSerializer.Deserialize<TMessage>(
    result.Message.Value, 
    JsonOptions);
```

**Producer (Serialization):**
```csharp
// KafkaProducer.PublishAsync()
var key = Encoding.UTF8.GetBytes(Guid.NewGuid().ToString());
var value = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
await producer.ProduceAsync(topic, new Message<byte[], byte[]> { Key = key, Value = value });
```

---

## Complete Example

### Registration

```csharp
public class Startup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddMessageQueues(configuration)
            // RabbitMQ
            .AddRabbitMqClientProviders()
            .AddRabbitMqConsumers()
            .AddRabbitMqProducers()
            
            // Kafka
            .AddKafkaClientProviders()
            .AddKafkaConsumers()
            .AddKafkaProducers()
            
            // Consumers
            .AddConsumer<OrderCreatedEvent, OrderCreatedConsumer>("order-created-consumer")
            .AddConsumer<OrderUpdatedEvent, OrderUpdatedConsumer>("order-updated-consumer")
            
            // Producers
            .AddProducer<OrderCreatedEvent>("kafka-connection", "order-created-producer")
            .AddProducer<NotificationEvent>("rabbitmq-connection", "notification-producer")
            
            .Build();
    }
}
```

### Consumer Implementation

```csharp
public class OrderCreatedConsumer : IConsumer<OrderCreatedEvent>
{
    private readonly ILogger<OrderCreatedConsumer> _logger;
    
    public OrderCreatedConsumer(ILogger<OrderCreatedConsumer> logger)
    {
        _logger = logger;
    }
    
    public async Task<bool> HandleMessageAsync(
        OrderCreatedEvent message, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing order {OrderId}", message.OrderId);
        
        // Process message
        // Return true to ACK, false to NACK
        return true;
    }
}
```

### Producer Usage

```csharp
public class OrderService
{
    private readonly IProducer<OrderCreatedEvent> _producer;
    
    public OrderService(
        [FromKeyedServices("order-created-producer")] IProducer<OrderCreatedEvent> producer)
    {
        _producer = producer;
    }
    
    public async Task CreateOrderAsync(Order order, CancellationToken ct)
    {
        // Create order...
        
        // Publish event
        await _producer.PublishAsync(new OrderCreatedEvent
        {
            OrderId = order.Id,
            CreatedAt = DateTime.UtcNow
        }, ct);
    }
}
```

---

## Troubleshooting

### Common Issues

1. **"No keyed service registered"**
   - Ensure connection name in configuration matches producer/consumer ConnectionName
   - Verify provider registration (AddKafkaClientProviders, AddRabbitMqClientProviders)

2. **Consumer not receiving messages**
   - Check Parallelism setting > 0
   - Verify topic/queue exists
   - Check consumer group ID (Kafka)

3. **Serialization errors**
   - Ensure message class has parameterless constructor
   - Verify JSON property names match (camelCase)

4. **Multiple consumers same message**
   - Kafka: Different consumer groups receive same messages
   - RabbitMQ: Each queue receives message once

### Debugging Tips

```csharp
// Check registered services
var consumerInitializer = services.GetKeyedService<IConsumerInitializer>("my-consumer");
var producer = services.GetKeyedService<IProducer<MyMessage>>("my-producer");
var clientProvider = services.GetKeyedService<IKafkaConsumerClientProvider>("connection-name");

// Check options
var optionsMonitor = services.GetRequiredService<IOptionsMonitor<ConsumerOptions>>();
var options = optionsMonitor.Get("my-consumer");
Console.WriteLine($"Connection: {options.ConnectionName}, Type: {options.Type}");
```