# Message queue usage (consumers & producers)

## Overview

This document shows the current (refactored) registration APIs for consumers and producers. Consumers are registered with both the message type and the concrete consumer type; producers are registered by connection name and a logical producer name.

## Example message and consumer

```csharp
public record OrderCreatedMessage(Guid OrderId, decimal Amount);

public class OrderCreatedConsumer : IConsumer<OrderCreatedMessage>
{
    private readonly ILogger<OrderCreatedConsumer> _logger;
    public OrderCreatedConsumer(ILogger<OrderCreatedConsumer> logger) => _logger = logger;

    public async Task<bool> HandleMessageAsync(MessageEnvelop<OrderCreatedMessage> envelop, CancellationToken cancellationToken = default)
    {
        // MessageEnvelop provides: Message, Key (optional), Headers (optional)
        _logger.LogInformation("Processing order {OrderId} amount {Amount}", 
            envelop.Message.OrderId, envelop.Message.Amount);
        await Task.Delay(50, cancellationToken);
        return true; // true = ACK, false = NACK
    }
}
```

## Registering consumers and producers

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureAsyncInitializers();

builder.Services.AddMessageQueues(builder.Configuration)
    .AddRabbitMqClientProviders()
    .AddRabbitMqConsumers()
    .AddRabbitMqProducers()

    .AddKafkaClientProviders()
    .AddKafkaConsumers()
    .AddKafkaProducers()

    // Register consumer: message type + implementation type, with optional consumer name
    .AddConsumer<OrderCreatedMessage, OrderCreatedConsumer>("OrderConsumer") // name optional, defaults to type name

    // Register producers: connection name + optional key selector
    .AddProducer<OrderCreatedMessage>("rabbitmq-connection")
    .AddProducer<OrderCreatedMessage>("kafka-connection")  // name defaults to connection name
    .AddProducer<OrderCreatedMessage>("kafka-connection", producerOptions: new ProducerOptions<IIntegrationEvent>()
    .AddProducer("kafka-connection",
        producerOptions: new ProducerOptions<OrderCreateMessage>()
        {
            KeySelector = m => m.Key,
            ResolveMessageTypeAtRuntime = true
        });

    .Build(); // Complete registration and enable hosted services

var app = builder.Build();

app.InvokeAsyncInitializers();

app.Run();
```

Notes:
- Consumer name is optional; if omitted, defaults to a safe type name derived from `TMessage`.
- Producer name is optional; if omitted, defaults to a safe type name derived from `TMessage`.
- The key selector (third parameter for `AddProducer`) is optional and used for Kafka partitioning.
- `Build()` is required to finalize registrations and start message-hosted services.

## Configuration (appsettings.json)

Example minimal configuration shape the message queues system expects:

```json
{
  "MessageQueues": {
    "Connections": [
      {
        "Name": "rabbitmq-connection",
        "Type": "rabbitmq",
        "Host": "localhost",
        "Port": 5672,
        "Username": "guest",
        "Password": "guest",
        "ServiceKey": "order-queue"
      },
      {
        "Name": "kafka-connection",
        "Type": "kafka",
        //"ServiceName": "kafka",
        "ConnectionString": "kafka:9092",
        "ServiceKey": "test-service",
        "Username": "test",
        "Password": "password",
        "Host": "localhost",
        "Port": 9092,
        "ErrorHandling": "DeadLetter",
        "RetryPolicy": {
          "InitialDelay": "00:00:02",
          "MaxDelay": "00:01:00",
          "MaxRetries": 5,
          "Type": "ExponentialBackoff"
        },
        "Parameters": { //Defaults for connection. Can be overriden in consumers
          "TopicConfiguration": {
            "NumPartitions": 12,
            "ReplicationFactor": 1,
            "RetentionTime": "7.00:00:00",
            "RetentionBytes": -1,
            "CleanUpPolicy": "CompactAndDelete",
            "DeleteRetentionTime": "1.00:00:00"
          },
          "DltTopicConfiguration": {
            "NumPartitions": 1,
            "ReplicationFactor": 1,
            "RetentionTime": "30.00:00:00",
            "RetentionBytes": -1,
            "CleanUpPolicy": "Delete",
            "DeleteRetentionTime": "1.00:00:00"
          }
        }
      }
    ],
    "Consumers": [
      {
        "Name": "OrderConsumer",
        "ConnectionName": "rabbitmq-connection",
        "Parallelism": 3,
        "ErrorHandling": "DeadLetter",
        "Parameters": {
          "SomeProviderSpecificKey": "value"
        }
      }
    ]
  }
}
```

## Key points

- **Consumer registration**: Use `AddConsumer<TMessage, TConsumer>(string? consumerName = null)`. `TConsumer` must implement `IConsumer<TMessage>`. The consumer is registered as a singleton.
- **Producer registration**: Use `AddProducer<TMessage>(connectionName, name = null, ProducerOptions<TMessage>? producerOptions = null)`. Producers are keyed by the connection name.
- **MessageEnvelop**: Consumer handlers receive `MessageEnvelop<TMessage>` containing `Message`, `Key` (optional), and `Headers` (optional).
- **Provider modules**: Call the appropriate provider modules before adding consumers/producers, e.g. `AddRabbitMqClientProviders()`, `AddRabbitMqConsumers()`, `AddRabbitMqProducers()` or the Kafka equivalents.
- **Build() required**: Call `.Build()` on the message queues builder to finalize registrations and enable the hosted services.

## How to resolve services at runtime

- Consumers are resolved by the runtime and instantiated per worker; you generally don't need to resolve consumers manually.
- Producers are registered as keyed services. To send messages from your application use the keyed service resolver:
- Producers can be configured to resolve message queue names in runtime to support common types. Can be helpfull when using single outbox to publish integration events

```csharp
var producer = app.Services.GetRequiredKeyedService<IProducer<OrderCreatedMessage>>("order-producer-kafka");

// Simple publish, key from keySelector will be applied if provided
await producer.PublishAsync(new OrderCreatedMessage(orderId, amount));

// Publish with explicit key (for Kafka partitioning)
await producer.PublishAsync(new OrderCreatedMessage(orderId, amount), key: orderId.ToString());

// Publish with explicit key and headers
var headers = new Dictionary<string, string> { ["CorrelationId"] = correlationId };
await producer.PublishAsync(new OrderCreatedMessage(orderId, amount), key: orderId.ToString(), headers: headers);
```

Or from constructor
```csharp
[FromKeyedServices("order-producer-kafka")] IProducer<OrderCreatedMessage> producer
```

## What gets registered (high level)

- An initializer for the consumer (creates queues/topics) keyed by the consumer name
- Message engine factories and worker hosted services that run the configured number of worker instances (based on `Parallelism`)
- Producer factories and keyed `IProducer<TMessage>` instances for publishing

## Troubleshooting checklist

- Did you call `.Build()` on the message queues builder?
- Did you register provider modules (e.g. `AddRabbitMqConsumers()` / `AddKafkaConsumers()`)?
- Does the consumer name passed to `AddConsumer<,>("name")` match the entry in `MessageQueues:Consumers` config?
- Is there a connection entry matching `ConnectionName` in `MessageQueues:Connections`?
- Are provider-specific parameters (e.g. partitions, replication) present when required by the provider?
- Check logs for startup messages from the message-hosted services and any initializer failures.
