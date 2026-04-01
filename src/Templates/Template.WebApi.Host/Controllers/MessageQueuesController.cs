using Microsoft.AspNetCore.Mvc;
using Minnaloushe.Core.ClientProviders.Abstractions;
using Minnaloushe.Core.MessageQueues.Abstractions;
using RabbitMQ.Client;
using Template.WebApi.Host.Consumer;
using VaultSharp;

namespace Template.WebApi.Host.Controllers;

[Route("api/[controller]")]
[ApiController]
public class MessageQueuesController() : ControllerBase
{
    [HttpPost("publishRabbitMqVault")]
    public async Task<IActionResult> PublishVaultCredentialsRabbit(
        [FromKeyedServices("rabbit-producer-static")] IProducer<TestMessage> producer,
        string message = "Test message rabbit vault")
    {
        await producer.PublishAsync(new TestMessage() { Data = message });

        return Ok();
    }

    [HttpPost("PublishRabbitMqConfig")]
    public async Task<IActionResult> PublishConfigCredentialsRabbit(
        [FromKeyedServices("rabbit-producer")] IProducer<TestMessage> producer,
        string message = "Test message rabbit config")
    {
        await producer.PublishAsync(new TestMessage() { Data = message });
        return Ok();
    }

    [HttpPost("PublishKafka")]
    public async Task<IActionResult> PublishKafkaTest([FromKeyedServices("kafka-producer")] IProducer<TestMessage> producer, string message)
    {
        await producer.PublishAsync(new TestMessage() { Data = message });
        return Ok();
    }
    [HttpPost("PublishRabbitBrokenTest")]
    public async Task<IActionResult> PublishRabbitBrokenTest([FromKeyedServices("rabbit-broken")] IProducer<TestBrokenMessage> producer)
    {
        await producer.PublishAsync(new TestBrokenMessage() { Data = "Test string" });
        return Ok();
    }

    [HttpPost("PublishKafkaBrokenTest")]
    public async Task<IActionResult> PublishBrokenTest([FromKeyedServices("kafka-broken")] IProducer<TestBrokenMessage> producer)
    {
        await producer.PublishAsync(new TestBrokenMessage() { Data = "Test string" });
        return Ok();
    }

    [HttpGet("GetBuffer")]
    public IActionResult GetBuffer([FromServices] IConsumer<TestMessage> consumer)
    {
        var buffer = (consumer as TestMessageConsumer)?.GetBufferSnapshot();
        return Ok(buffer);
    }

    [HttpGet("GetDynamicCredentials")]
    public async Task<IActionResult> GetDynamicCredentials([FromServices] IClientProvider<IVaultClient> vaultClient,
        string roleName = "my-rabbitmq-role",
        string host = "rabbitmq",
        ushort port = 5672,
        string exchangeName = "test-exchange")
    {
        using var lease = vaultClient.Acquire();

        var creds = await lease.Client.V1.Secrets.RabbitMQ.GetCredentialsAsync(roleName);

        var connectionFactory = new ConnectionFactory()
        {
            HostName = host,
            Port = port,
            UserName = creds.Data.Username,
            Password = creds.Data.Password
        };

        await using var connection = await connectionFactory.CreateConnectionAsync();

        await using var channel = await connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(exchangeName, "fanout", true, false);

        return Ok(new
        {
            Username = creds.Data.Username,
            Password = creds.Data.Password
        });
    }
}